using Amazon.Runtime.Internal.Util;
using Azure.Search.Documents.Indexes;
using Azure;
using Azure.Search.Documents.Indexes.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using SharedRAG.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using MongoDB01.Models;
using System.Text.Json.Nodes;
using Azure.Search.Documents.Models;
using Azure.Search.Documents;

namespace MongoDB01
{
    internal class MainAISearch : IHostedService
    {
        private readonly ILogger<MainAISearch> _logger;
        private readonly OpenAIService _ai;
        private readonly BlobService _blob;
        private AzureAISearchService _searchService;
        public MainAISearch(
            ILogger<MainAISearch> logger,
            OpenAIService ai,
            AzureAISearchService searchService,
            BlobService blobClient)
        {
            _logger = logger;
            _ai = ai;
            _searchService = searchService;
            _blob = blobClient;
        }
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            Console.WriteLine("MainAISearch Started");
            // await _searchService.CreateIndexAsync(DefineIndex());
            //await _searchService.UploadDocumentsAsync(await GetDocumentsAsync());
            await SearchPropertiesAsync("Embedding", "two bedrooms with view of the sea");
            Console.WriteLine("done");
            await StopAsync(cancellationToken);
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            Console.WriteLine("Stopped");
            return Task.CompletedTask;
        }

        private SearchIndex DefineIndex()
        {
            string vectorSearchProfileName = "my-vector-profile";
            string vectorSearchHnswConfig = "my-hsnw-vector-config";
            int modelDimensions = 1536;

            string indexName = "properties";
            return new SearchIndex(indexName)
            {
                Fields =
                {
                    new SimpleField("Id", SearchFieldDataType.String) { IsKey = true, IsFilterable = true, IsSortable = true, IsFacetable = true },
                    new SearchableField("Name") { IsFilterable = true, IsSortable = true },
                    new SearchableField("Description") { IsFilterable = true },
                    new VectorSearchField("Embedding", modelDimensions, vectorSearchProfileName)
                },
                VectorSearch = new()
                {
                    Profiles =
                    {
                        new VectorSearchProfile(vectorSearchProfileName, vectorSearchHnswConfig)
                    },
                                Algorithms =
                    {
                        new HnswAlgorithmConfiguration(vectorSearchHnswConfig)
                    }
                },
            };
        }

        private async Task<IEnumerable<PropertyIndexModel>> GetDocumentsAsync()
        {
            var props = new List<PropertyIndexModel>();
            var blobs = await _blob.DownloadBlobsAsync();
            _logger.LogInformation("Downloadin blobs");
            foreach (var blob in blobs)
            {
                var doc = JsonObject.Parse(blob);
                var prop = new PropertyIndexModel
                {
                    Id = doc["id"].ToString(),
                    Name = doc["name"].GetValue<string>(),
                    Description = doc["description"].GetValue<string>(),
                };
                props.Add(prop);
            }
            _logger.LogInformation($"Downloaded: {props.Count} blobs");
            _logger.LogInformation("Getting embeddings");
            foreach (var prop in props)
            {
                var (vector, tokens) = await _ai.GetEmbeddingsAsync(String.Empty, prop.Description);
                prop.Embedding = vector;
            }
            _logger.LogInformation("Got embeddings");
            return props;
        }

        private async Task SearchPropertiesAsync(string indexName, string filter)
        {
            var (vector, tokens) = await _ai.GetEmbeddingsAsync(string.Empty, filter);
            ReadOnlyMemory<float> vectorizedResult = new ReadOnlyMemory<float>(vector);
            var props = await _searchService.VectorSearchAsync<PropertyIndexModel>(indexName, vectorizedResult);
            foreach (var prop in props)
            {
                Console.WriteLine($"Id: {prop.Id}, Name: {prop.Name}, Description: {prop.Description}");
                Console.WriteLine("==============================================================");
            }
        }
    }
}
