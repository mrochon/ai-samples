using Amazon.Runtime.Internal.Util;
using Azure.AI.OpenAI;
using Azure;
using Azure.Search.Documents.Indexes.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Search.Documents;
using Microsoft.Extensions.Options;
using SharedRAG.Models;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Models;

namespace SharedRAG.Services
{
    public class AzureAISearchService
    {
        private ILogger<AzureAISearchService> _logger;
        private IOptions<AzureAISearchOptions> _options;
        private readonly SearchClient _client;
        public AzureAISearchService(
            ILogger<AzureAISearchService> logger,
            IOptions<AzureAISearchOptions> options)
        {
            ArgumentNullException.ThrowIfNull(logger);
            ArgumentNullException.ThrowIfNull(options);
            ArgumentNullException.ThrowIfNull(options.Value);
            ArgumentNullException.ThrowIfNull(options.Value.Endpoint);
            ArgumentNullException.ThrowIfNull(options.Value.IndexName);
            ArgumentNullException.ThrowIfNull(options.Value.AdminKey);
            _logger = logger;
            _options = options;
            var aiOptions = new OpenAIClientOptions()
            {
                Retry =
                {
                    Delay = TimeSpan.FromSeconds(2),
                    MaxRetries = 10,
                    Mode = RetryMode.Exponential
                }
            };
            _client = new(new Uri(_options.Value.Endpoint), _options.Value.IndexName, new AzureKeyCredential(_options.Value.AdminKey));
        }

        public async Task CreateIndexAsync(SearchIndex index)
        {
            SearchIndexClient indexClient = new(new Uri(_options.Value.Endpoint), new AzureKeyCredential(_options.Value.AdminKey));
            await indexClient.CreateIndexAsync(index);
        }

        public async Task UploadDocumentsAsync<T>(IEnumerable<T> documents) where T : class
        {
            await _client.IndexDocumentsAsync(IndexDocumentsBatch.Upload(documents));
        }

        public async Task<IEnumerable<T>> VectorSearchAsync<T>(string indexName, ReadOnlyMemory<float> vectorizedValue) where T : class
        {
            _logger.LogTrace($"Vector Search using: {indexName}");
            List<T> docs = new();
            SearchResults<T> response = await _client.SearchAsync<T>(
                new SearchOptions
                {
                    VectorSearch = new()
                    {
                        Queries = { new VectorizedQuery(vectorizedValue) { KNearestNeighborsCount = 3, Fields = { indexName } } }
                    }
                });

            int count = 0;
            _logger.LogTrace("Processing search results");
            await foreach (SearchResult<T> result in response.GetResultsAsync())
            {
                count++;
                T doc = result.Document;
                docs.Add(doc);
            }
            _logger.LogTrace($"Total number of search results:{count}");
            return docs;
        }
    }
}
