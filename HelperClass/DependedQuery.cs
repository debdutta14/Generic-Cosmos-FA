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

namespace CosmosUpdater.HelperClass;

public class DependedQuery
{
    private readonly CosmosClient _cosmosClient;
    private readonly ILogger _logger;
    private readonly string databaseId;
    private readonly string containerId;

    public DependedQuery(CosmosClient cosmosClient, ILoggerFactory loggerFactory, IConfiguration configuration)
    {
        _cosmosClient = cosmosClient;
        _logger = loggerFactory.CreateLogger<DependedQuery>();
        databaseId = configuration["CosmosDbDatabaseName"]!;
        containerId = configuration["CosmosDbContainerName"]!;
    }

    public async Task<bool> ExecuteDependedQueryAsync(JObject record)
    {
        var container = _cosmosClient.GetContainer(databaseId, containerId);
        var queryDefinition = new QueryDefinition(@"
                SELECT TOP 1 VALUE true
                FROM c
                WHERE c.sku = @sku
                AND c.storeId = @storeId
                AND c.recordType = @recordType
                AND c.status = 'Completed'
                AND c.timestamp > @timestamp")
                .WithParameter("@sku", record["sku"]?.ToString())
                .WithParameter("@storeId", record["storeId"]?.ToString())
                .WithParameter("@recordType", record["recordType"]?.ToString())
                .WithParameter("@timestamp", record["timestamp"]?.ToObject<long>()
                );
        _logger.LogInformation(
            "sku={sku}, storeId={storeId}, recordType={recordType}, timestamp={timestamp}",
            record["sku"]?.ToString(),
            record["storeId"]?.ToString(),
            record["recordType"]?.ToString(),
            record["timestamp"]?.ToObject<long>());

        var iterator = container.GetItemQueryIterator<bool>(queryDefinition);

        if (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            return response.Any();
        }
        return false;
    }
}
