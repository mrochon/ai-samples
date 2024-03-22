using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using Azure.Search.Documents.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using SearchApps.Models;
using Azure;

#pragma warning disable SKEXP0010

namespace SearchApps.Apps
{
    public class HelloWorld : IHostedService
    {
        private readonly ILogger<HelloWorld> _logger;
        private readonly IOptions<AzureAISearchOptions> _searchOptions;
        public HelloWorld(
            ILogger<HelloWorld> logger,
            IOptions<AzureAISearchOptions> searchOptions)
        {
            _logger = logger;
            _searchOptions = searchOptions;
            ArgumentNullException.ThrowIfNull(logger);
            ArgumentNullException.ThrowIfNull(_searchOptions.Value.Key);
            ArgumentNullException.ThrowIfNull(_searchOptions.Value.Endpoint);
        }
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogTrace($"{this.GetType().Name} Started");

            Uri endpoint = new Uri(_searchOptions.Value.Endpoint);
            AzureKeyCredential credential = new AzureKeyCredential(_searchOptions.Value.AdminKey);
            SearchIndexClient indexClient = new SearchIndexClient(endpoint, credential);

            Response<SearchServiceStatistics> stats = await indexClient.GetServiceStatisticsAsync();
            Console.WriteLine($"You are using {stats.Value.Counters.IndexCounter.Usage} indexes.");

            // Create an invalid SearchClient
            string fakeIndexName = "doesnotexist";
            SearchClient searchClient = new SearchClient(endpoint, fakeIndexName, credential);
            try
            {
                var n = await searchClient.GetDocumentCountAsync();
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                Console.WriteLine("Index wasn't found.");
            }

            _logger.LogTrace($"{this.GetType().Name} Done");
            await StopAsync(cancellationToken);
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogTrace($"{this.GetType().Name} Stopping");
            return Task.CompletedTask;
        }
    }
}
