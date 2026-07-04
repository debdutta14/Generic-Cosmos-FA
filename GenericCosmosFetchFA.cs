using System.Net;
using System.Reflection;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using CosmosUpdater.Models;
using Microsoft.Extensions.Configuration;

namespace CosmosUpdater.Functions
{
    public class GenericCosmosFetchFA
    {
        private readonly CosmosClient _cosmosClient;
        private readonly ILogger _logger;
        private readonly string databaseId;
        private readonly string containerId;

        private static readonly HashSet<string> AllowedFields = new HashSet<string>(
        typeof(Price_Product_DTO).GetProperties()
        .Select(p => p.Name)
        .OfType<string>()
        .Where(name => name != "id" && !name.StartsWith("_")),
        StringComparer.Ordinal);

        private static readonly HashSet<string> ImmutableFields = new HashSet<string>(
            typeof(Price_Product_DTO).GetProperties()
            .Select(p => p.Name)
            .OfType<string>()
            .Where(name => name == "id" || name.StartsWith("_") || name == "recordType" || name == "sku" || name == "storeId"),
            StringComparer.Ordinal);

        public GenericCosmosFetchFA(CosmosClient cosmosClient, ILoggerFactory loggerFactory, IConfiguration configuration)
        {
            _cosmosClient = cosmosClient;
            _logger = loggerFactory.CreateLogger<GenericCosmosFetchFA>();
            databaseId = configuration["CosmosDbDatabaseName"]!;
            containerId = configuration["CosmosDbContainerName"]!;
        }

        [Function("UpdateCosmosRecords")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
        {
            _logger.LogInformation("Processing Cosmos Generic FA.");

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var payload = JsonConvert.DeserializeObject<BulkUpdatePayload>(requestBody);

            if (payload == null || string.IsNullOrWhiteSpace(payload.Query) || payload.RequiredChange == null)
            {
                var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequest.WriteStringAsync("Invalid payload. 'query' and 'requiredChange' are mandatory.");
                return badRequest;
            }

            var invalidKeys = payload.RequiredChange.Keys
                .Where(key => !AllowedFields.Contains(key))
                .ToList();

            if (invalidKeys.Any())
            {
                _logger.LogWarning("Validation failed. Invalid keys requested: {Keys}", string.Join(",", invalidKeys));

                var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);

                // Provide a highly descriptive error back to the user
                string errorMessage =
                    $"Validation Error: The following fields are invalid, immutable, or incorrectly cased: [{string.Join(", ", invalidKeys)}].\n" +
                    $"Allowed fields for modification are strictly: [{string.Join(", ", AllowedFields)}].";

                await badRequest.WriteStringAsync(errorMessage);
                return badRequest;
            }
            else if (payload.RequiredChange.Keys.Any(key => ImmutableFields.Contains(key)))
            {
                var immutableKeys = payload.RequiredChange.Keys.Where(key => ImmutableFields.Contains(key)).ToList();
                _logger.LogWarning("Attempted to modify immutable fields: {Keys}", string.Join(",", immutableKeys));

                var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                string errorMessage =
                    $"Validation Error: The following fields are immutable and cannot be modified: [{string.Join(", ", immutableKeys)}].\n" +
                    $"Immutable fields include: [{string.Join(", ", ImmutableFields)}].";

                await badRequest.WriteStringAsync(errorMessage);
                return badRequest;
            }

    
            var container = _cosmosClient.GetContainer(databaseId, containerId);

            var queryDefinition = new QueryDefinition(payload.Query);
            var queryOptions = new QueryRequestOptions { MaxItemCount = payload.Limit };
            _logger.LogInformation("Executing query.");
            var iterator = container.GetItemQueryIterator<Price_Product_DTO>(queryDefinition, requestOptions: queryOptions);
            List<Price_Product_DTO> documentsToUpdate = new List<Price_Product_DTO>();

            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                documentsToUpdate.AddRange(response);

                if (documentsToUpdate.Count >= payload.Limit)
                {
                    documentsToUpdate = documentsToUpdate.Take(payload.Limit).ToList();
                    break;
                }
            }

            if (!documentsToUpdate.Any())
            {
                var notFound = req.CreateResponse(HttpStatusCode.OK);
                await notFound.WriteStringAsync("No records found matching the query.");
                return notFound;
            }

            List<Task> upsertTasks = new List<Task>();

            foreach (var document in documentsToUpdate)
            {
                var jDoc = JObject.FromObject(document);

                foreach (var change in payload.RequiredChange)
                {
                    jDoc[change.Key] = JToken.FromObject(change.Value);
                }

                var updatedDocument = jDoc.ToObject<Price_Product_DTO>();

                if (!string.IsNullOrEmpty(updatedDocument?.id))
                {
                    upsertTasks.Add(container.UpsertItemAsync(updatedDocument, new PartitionKey(updatedDocument.id)));
                }
            }

            await Task.WhenAll(upsertTasks);

            var successResponse = req.CreateResponse(HttpStatusCode.OK);
            await successResponse.WriteStringAsync($"Successfully updated {upsertTasks.Count} records.");
            return successResponse;
        }
    }
}