using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Models;
using Azure.Search.Documents;
using Azure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json.Serialization;
using SKApps.Models;
using Azure.AI.OpenAI;
using Azure.Core;
using Microsoft.Extensions.Options;
using Amazon.Runtime.Internal.Util;
using Microsoft.Extensions.Logging;

namespace SKApps.Services
{
    public interface ISearchService
    {
        Task<string?> SearchAsync(
            string collectionName,
            ReadOnlyMemory<float> vector,
            List<string>? searchFields = null,
            CancellationToken cancellationToken = default);
    }


    public sealed class AzureAISearchService : ISearchService
    {
        ILogger<AzureAISearchService> _logger;
        private readonly List<string> _defaultVectorFields = new() { "vector" };
        private readonly SearchClient _client;

        public AzureAISearchService(
            ILogger<AzureAISearchService> logger,
            IOptions<AzureAISearchOptions> options)
        {
            _logger = logger;
            var aiOptions = new OpenAIClientOptions()
            {
                Retry =
                {
                    Delay = TimeSpan.FromSeconds(2),
                    MaxRetries = 10,
                    Mode = RetryMode.Exponential
                }
            };
            _client = new(new Uri(options.Value.Endpoint), options.Value.IndexName, new AzureKeyCredential(options.Value.Key));
        }

        public async Task<string?> SearchAsync(string collectionName, ReadOnlyMemory<float> vector, List<string>? searchFields = null, CancellationToken cancellationToken = default)
        {
            _logger.LogTrace($"Vector Search using: {collectionName}");

            List<string> fields = searchFields is { Count: > 0 } ? searchFields : this._defaultVectorFields;
            VectorizedQuery vectorQuery = new(vector);
            fields.ForEach(field => vectorQuery.Fields.Add(field));
            SearchOptions searchOptions = new() { VectorSearch = new() { Queries = { vectorQuery } } };
            Response<SearchResults<IndexSchema>> response = await _client.SearchAsync<IndexSchema>(searchOptions, cancellationToken);
            List<IndexSchema> results = new();
            await foreach (SearchResult<IndexSchema> result in response.Value.GetResultsAsync())
            {
                results.Add(result.Document);
            }
            // Return text from first result.
            // In real applications, the logic can check document score, sort and return top N results
            // or aggregate all results in one text.
            // The logic and decision which text data to return should be based on business scenario. 
            return results.FirstOrDefault()?.Chunk;
        }

        public sealed class IndexSchema
        {
            [JsonPropertyName("chunk_id")]
            public string ChunkId { get; set; }

            [JsonPropertyName("parent_id")]
            public string ParentId { get; set; }

            [JsonPropertyName("chunk")]
            public string Chunk { get; set; }

            [JsonPropertyName("title")]
            public string Title { get; set; }

            [JsonPropertyName("vector")]
            public ReadOnlyMemory<float> Vector { get; set; }
        }
    }
}

