using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices(services =>
    {
        services.AddSingleton(sp =>
        {
            string connectionString = Environment.GetEnvironmentVariable("CosmosDbConnectionString")!;
            
            var options = new CosmosClientOptions
            {
                AllowBulkExecution = true,
                MaxRetryAttemptsOnRateLimitedRequests = 3,
                MaxRetryWaitTimeOnRateLimitedRequests = TimeSpan.FromSeconds(10)
            };

            return new CosmosClient(connectionString, options);
        });
    })
    .Build();

host.Run();