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
using MongoDB.Driver.Linq;
using MongoDB.Driver;

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
        IOptions<AzureAISearchOptions> _options;

        public AzureAISearchService(
            ILogger<AzureAISearchService> logger,
            IOptions<AzureAISearchOptions> options)
        {
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
            Count = 3;
        }

        public int Count { get; set; }

        public async Task<string?> SearchAsync(string collectionName, ReadOnlyMemory<float> vector, List<string>? searchFields = null, CancellationToken cancellationToken = default)
        {
            _logger.LogTrace($"Vector Search using: {collectionName}");
            SearchClient client = new(new Uri(_options.Value.Endpoint), collectionName, new AzureKeyCredential(_options.Value.Key));

            var searchOptions = new SearchOptions
            {

                VectorSearch = new()
                {
                    Queries = { new VectorizedQuery(vector) { KNearestNeighborsCount = Count } }
                }
            };
            searchFields ??= _defaultVectorFields;
            searchFields.ForEach(field => searchOptions.VectorSearch.Queries.First().Fields.Add(field));
            Response<SearchResults<PropertyIndexModel>> response = await client.SearchAsync<PropertyIndexModel>(searchOptions, cancellationToken);
            StringBuilder results = new();
            var docs = response.Value.GetResultsAsync().ToBlockingEnumerable().OrderByDescending(d => d.Score);
            foreach (var doc in docs)
            {
                results.Append(doc.Document.Description);
                results.AppendLine();
                results.Append("====");
                results.AppendLine();
            }
            // In real applications, the logic can check document score, sort and return top N results
            // or aggregate all results in one text.
            // The logic and decision which text data to return should be based on business scenario. 
            _logger.LogTrace($"Vector Search result length: {results.Length}");
            return results.ToString();
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

        public class PropertyIndexModel
        {
            public string? Id { get; set; }
            public string? Name { get; set; }
            public string? Description { get; set; }
            //public ReadOnlyMemory<float> Embedding { get; set; }
        }
    }
}

