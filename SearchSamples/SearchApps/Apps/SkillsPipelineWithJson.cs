using Azure;
using Azure.Search.Documents.Models;
using Azure.Search.Documents;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SearchApps.Models;
using System;
using System.Text;
using System.Text.Json;
using Azure.AI.OpenAI;
using Azure.Core;


// https://learn.microsoft.com/en-us/azure/search/cognitive-search-tutorial-blob-dotnet
namespace SearchApps.Apps
{
    public class SkillsPipelineWithJson : IHostedService
    {
        private readonly ILogger<SkillsPipelineWithJson> _logger;
        private readonly AzureAISearchOptions _searchOptions;
        private readonly OpenAIOptions _aiOptions;
        private readonly BlobOptions _blobOptions;
        private readonly Uri _endpoint;
        private readonly AzureKeyCredential _credential;
        private readonly string _searchAPIBase = "https://api.cognitive.microsoft.com/search/v1.0";

        private HttpClient _http;

        private readonly string _indexName;
        private readonly string _indexerName;
        private readonly string _dataSourceName;
        public SkillsPipelineWithJson(
            ILogger<SkillsPipelineWithJson> logger,
            IOptions<AzureAISearchOptions> searchOptions,
            IOptions<OpenAIOptions> aiOptions,
            IOptions<BlobOptions> blobOptions)
        {
            _logger = logger;
            _searchOptions = searchOptions.Value;
            _aiOptions = aiOptions.Value;
            _blobOptions = blobOptions.Value;
            _blobOptions.ConnectionString = String.Format($"DefaultEndpointsProtocol=https;AccountName={_blobOptions.AccountName};AccountKey={_blobOptions.Key};EndpointSuffix=core.windows.net");

            _indexName = "reviews-sem";
            _indexerName = "reviews-sem";
            _dataSourceName = "reviews-sem";
        }
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogTrace($"{this.GetType().Name} Started");

            _http = new HttpClient() { BaseAddress = _endpoint };
            _http.DefaultRequestHeaders.Add("api-key", _searchOptions.AdminKey);

            /* ============================================================ */
            await CreateDataSourceAsync();
            await CreateIndexAsync();
            await CreateIndexerAsync();
            /* ============================================================ */

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
            _logger.LogTrace("Create datasource");
            try
            {
                var json = File.ReadAllText("./Data/HotelReviewsSkillset/datasource.json");
                var sb = new StringBuilder(json);
                sb.Replace("DATASOURCE_NAME", _dataSourceName);
                sb.Replace("CONNECTION_STRING", _blobOptions.ConnectionString);
                sb.Replace("CONTAINER_NAME", "hotelreviews");
                json = sb.ToString();
                var resp = await _http.PutAsync($"https://{_searchOptions.ServiceName}.search.windows.net/datasources/{_indexName}?api-version=2020-06-30", new StringContent(json, Encoding.UTF8, "application/json"));
                if (resp.IsSuccessStatusCode)
                {
                    _logger.LogTrace("Datasource created");
                }
                else
                {
                    _logger.LogError($"Failed to create datasource: {await resp.Content.ReadAsStringAsync()}");
                    
                    throw new Exception("Failed to create datasource");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        private async Task CreateIndexerAsync()
        {
            _logger.LogTrace("Create indexer");
            try
            {
                var json = File.ReadAllText("./Data/HotelReviewsSkillset/indexer.json");
                var sb = new StringBuilder(json);
                sb.Replace("INDEXER_NAME", _indexerName);
                sb.Replace("DATASOURCE_NAME", _dataSourceName);
                sb.Replace("INDEX_NAME", _indexName);
                json = sb.ToString();
                var resp = await _http.PutAsync($"https://{_searchOptions.ServiceName}.search.windows.net/indexers/{_indexerName}?api-version=2020-06-30", new StringContent(json, Encoding.UTF8, "application/json"));
                if (resp.IsSuccessStatusCode)
                {
                    _logger.LogTrace("Indexer created");
                }
                else
                {
                    _logger.LogError($"Failed to create Indexer: {await resp.Content.ReadAsStringAsync()}");

                    throw new Exception("Failed to create indexer");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        private async Task CreateIndexAsync()
        {
            _logger.LogTrace("Create index");
            try
            {
                var json = File.ReadAllText("./Data/HotelReviewsSkillset/index.json");
                var sb = new StringBuilder(json);
                sb.Replace("INDEX_NAME", _indexName);
                sb.Replace("RESOURCE_URI", _aiOptions.Endpoint);
                sb.Replace("API_KEY", _aiOptions.Key);
                sb.Replace("DEPLOYMENT_ID", _aiOptions.EmbeddingsDeployment);
                json = sb.ToString();
                // https://learn.microsoft.com/en-us/azure/search/vector-search-how-to-configure-vectorizer
                var resp = await _http.PutAsync($"https://{_searchOptions.ServiceName}.search.windows.net/indexes/{_indexName}?api-version=2023-10-01-preview&allowIndexDowntime=true", new StringContent(json, Encoding.UTF8, "application/json"));
                if (resp.IsSuccessStatusCode)
                {
                    _logger.LogTrace("Index created");
                }
                else
                {
                    _logger.LogError($"Failed to create index: {resp.ReasonPhrase}/{await resp.Content.ReadAsStringAsync()}");
                    throw new Exception("Failed to create index");
                    // The request is invalid. Details: Cannot find nested property 'vectorizers' on the resource type 'Microsoft.Azure.Search.V2023_11_01.VectorSearch'
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }  
        
        private async Task CreateSkillset()
        {
            _logger.LogTrace("Creating skillset");
            try
            {
                var json = File.ReadAllText("./Data/HotelReviewsSkillset/skillset.json");
                var sb = new StringBuilder(json);
                sb.Replace("SKILLSET_NAME", _indexName);
                sb.Replace("CONNEVCTION_STRING", _aiOptions.Endpoint);
                sb.Replace("CONTAINER_NAME", _aiOptions.Key);

                json = sb.ToString();
                var resp = await _http.PutAsync($"https://{_searchOptions.ServiceName}.search.windows.net/indexes/{_indexName}?api-version=2023-10-01-preview&allowIndexDowntime=true", new StringContent(json, Encoding.UTF8, "application/json"));
                if (resp.IsSuccessStatusCode)
                {
                    _logger.LogTrace("Index created");
                }
                else
                {
                    _logger.LogError($"Failed to create index: {resp.ReasonPhrase}/{await resp.Content.ReadAsStringAsync()}");
                    throw new Exception("Failed to create index");
                    // The request is invalid. Details: Cannot find nested property 'vectorizers' on the resource type 'Microsoft.Azure.Search.V2023_11_01.VectorSearch'
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

        }

        private async Task SemanticSearchAsync(string query)
        {
            SearchClient searchClient = new(_endpoint, _indexName, _credential);
            ReadOnlyMemory<float> vectorizedResult = await GetEmbeddingsAsync(query);
            SearchResults<Hotel> response = await searchClient.SearchAsync<Hotel>(
                 query,
                 new SearchOptions
                 {
                     VectorSearch = new()
                     {
                         Queries = { new VectorizedQuery(vectorizedResult) { KNearestNeighborsCount = 3, Fields = { "DescriptionVector" } } }
                     },
                     SemanticSearch = new()
                     {
                         SemanticConfigurationName = "my-semantic-config",
                         QueryCaption = new(QueryCaptionType.Extractive),
                         QueryAnswer = new(QueryAnswerType.Extractive)
                     },
                     QueryType = SearchQueryType.Semantic,
                 });

            int count = 0;
            Console.WriteLine($"Semantic Hybrid Search Results:");

            Console.WriteLine($"\nQuery Answer:");
            foreach (QueryAnswerResult result in response.SemanticSearch.Answers)
            {
                Console.WriteLine($"Score            : {result.Score}");
                Console.WriteLine($"Answer Highlights: {result.Highlights}");
                Console.WriteLine($"Answer Text      : {result.Text}");
            }

            await foreach (SearchResult<Hotel> result in response.GetResultsAsync())
            {
                count++;
                Hotel doc = result.Document;
                Console.WriteLine($"{doc.HotelId}: {doc.HotelName}");

                if (result.SemanticSearch.Captions != null)
                {
                    Console.WriteLine($"Score            : {result.Score}");
                    var caption = result.SemanticSearch.Captions.FirstOrDefault();
                    if (caption.Highlights != null && caption.Highlights != "")
                    {
                        Console.WriteLine($"Caption Highlights: {caption.Highlights}");
                    }
                    else
                    {
                        Console.WriteLine($"Caption Text      : {caption.Text}");
                    }
                }
            }
            Console.WriteLine($"Total number of search results:{count}");
        }
        public async Task<float[]> GetEmbeddingsAsync(string input)
        {
            float[] embedding = new float[0];
            var client = new OpenAIClient(new Uri(_aiOptions.Endpoint), new AzureKeyCredential(_aiOptions.Key), new OpenAIClientOptions()
            {
                Retry =
                {
                    Delay = TimeSpan.FromSeconds(2),
                    MaxRetries = 10,
                    Mode = RetryMode.Exponential
                }
            });
            try
            {
                var options = new EmbeddingsOptions(_aiOptions.EmbeddingsDeployment, new List<string> { input });
                var response = await client.GetEmbeddingsAsync(options);
                var embeddings = response.Value;
                return embeddings.Data[0].Embedding.ToArray();
            }
            catch (Exception ex)
            {
                string message = $"OpenAiService.GetEmbeddingsAsync(): {ex.Message}";
                _logger.LogError(message);
                throw;
            }
        }
    }
}
