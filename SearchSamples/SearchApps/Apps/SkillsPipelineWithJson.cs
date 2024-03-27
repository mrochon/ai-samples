using Azure;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SearchApps.Models;
using System;
using System.Text;
using System.Text.Json;


// https://learn.microsoft.com/en-us/azure/search/cognitive-search-tutorial-blob-dotnet
namespace SearchApps.Apps
{
    public class SkillsPipelineWithJson : IHostedService
    {
        private readonly ILogger<SkillsPipelineWithJson> _logger;
        private readonly AzureAISearchOptions _searchOptions;
        private readonly BlobOptions _blobOptions;
        private readonly Uri _endpoint;
        private readonly AzureKeyCredential _credential;

        private HttpClient _http;

        private readonly string _indexName;
        private readonly string _indexerName;
        private readonly string _dataSourceName;
        public SkillsPipelineWithJson(
            ILogger<SkillsPipelineWithJson> logger,
            IOptions<AzureAISearchOptions> searchOptions,
            IOptions<BlobOptions> blobOptions)
        {
            _logger = logger;
            _searchOptions = searchOptions.Value;
            _blobOptions = blobOptions.Value;
            _blobOptions.ConnectionString = String.Format($"DefaultEndpointsProtocol=https;AccountName={_blobOptions.Value.AccountName};AccountKey={_blobOptions.Value.Key};EndpointSuffix=core.windows.net");

            _indexName = "hotelsreview";
            _indexerName = "hotelreviewsfromcvs";
            _dataSourceName = "hotelreviewsincsv";
        }
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogTrace($"{this.GetType().Name} Started");

            _http = new HttpClient() { BaseAddress = _endpoint };
            _http.DefaultRequestHeaders.Add("api-key", _searchOptions.AdminKey);

            _logger.LogTrace($"{this.GetType().Name} Done");
            await StopAsync(cancellationToken);
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogTrace($"{this.GetType().Name} Stopping");
            return Task.CompletedTask;
        }

        private async Task CreateDataSourceAsync()
        {
            var datasource = new
            {
                name = _dataSourceName,
                type = "azureblob",
                credentials = new { connectionString = _blobOptions.ConnectionString },
                container = new { name = _blobOptions.ContainerName },
            };

            var resp = await _http.PostAsync($"/datasources?api-version=2020-06-30", new StringContent(JsonSerializer.Serialize(datasource), Encoding.UTF8, "application/json"));
            if(!resp.IsSuccessStatusCode)
            {
                _logger.LogError($"Failed to create datasource: {await resp.Content.ReadAsStringAsync()}");
                throw new Exception("Failed to create datasource");
            }
        }

        private async Task CreateIndexer()
        {

        }
    }
}
