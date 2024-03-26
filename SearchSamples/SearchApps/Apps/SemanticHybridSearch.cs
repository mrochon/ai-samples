using Azure.Search.Documents.Indexes.Models;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents;
using Azure;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SearchApps.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MongoDB.Driver;
using Azure.Core.Pipeline;
using Azure.Search.Documents.Models;
using System.Text.Json;
using Azure.AI.OpenAI;
using Microsoft.VisualBasic.FileIO;
using Azure.Core;
using System.Diagnostics;

// https://github.com/Azure/azure-sdk-for-net/blob/Azure.Search.Documents_11.5.1/sdk/search/Azure.Search.Documents/samples/Sample07_VectorSearch_UsingSemanticHybridQuery.md


namespace SearchApps.Apps
{
    public class SemanticHybridSearch : IHostedService
    {
        private readonly ILogger<SemanticHybridSearch> _logger;
        private readonly IOptions<AzureAISearchOptions> _searchOptions;
        private readonly IOptions<OpenAIOptions> _aiOptions;
        private readonly IOptions<BlobOptions> _blobOptions;
        private readonly SearchIndexClient _indexClient;
        private readonly Uri _endpoint;
        private readonly AzureKeyCredential _credential;

        private readonly string _indexName;
        private readonly string _dataSourceConnectionName;
        public SemanticHybridSearch(
            ILogger<SemanticHybridSearch> logger,
            IOptions<AzureAISearchOptions> searchOptions,
            IOptions<OpenAIOptions> aiOptions,
            IOptions<BlobOptions> blobOptions)
        {
            _logger = logger;
            _searchOptions = searchOptions;
            _aiOptions = aiOptions;
            _blobOptions = blobOptions;
            ArgumentNullException.ThrowIfNull(logger);
            ArgumentNullException.ThrowIfNull(_searchOptions.Value.Key);
            ArgumentNullException.ThrowIfNull(_searchOptions.Value.Endpoint);

            _endpoint = new Uri(_searchOptions.Value.Endpoint);
            _credential = new AzureKeyCredential(_searchOptions.Value.AdminKey);
            _indexClient = new SearchIndexClient(_endpoint, _credential);

            _indexName = "hotels";
            _dataSourceConnectionName = "hotels";
        }
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogTrace($"{this.GetType().Name} Started");

            await CreateIndexAsync();

            await LoadIndexAsync();

            await SemanticSearchAsync("Is there any hotel located on the main commercial artery of the city in the heart of New York?");

            await ExecuteHybdridSearchAsync("Is there any hotel located on the main commercial artery of the city in the heart of New York?");
            
            _logger.LogTrace($"{this.GetType().Name} Done");
            await StopAsync(cancellationToken);
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogTrace($"{this.GetType().Name} Stopping");

            return Task.CompletedTask;
        }
        private async Task CreateIndexAsync()
        {
            try
            {
                _indexClient.GetIndex(_indexName);
                _indexClient.DeleteIndex(_indexName);
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                //if the specified index not exist, 404 will be thrown.
            }

            string vectorSearchProfileName = "my-vector-profile";
            string vectorSearchHnswConfig = "my-hsnw-vector-config";
            int modelDimensions = 1536;

            SearchIndex searchIndex = new(_indexName)
            {
                Fields =
                {
                    new SimpleField("HotelId", SearchFieldDataType.String) { IsKey = true, IsFilterable = true, IsSortable = true, IsFacetable = true },
                    new SearchableField("HotelName") { IsFilterable = true, IsSortable = true },
                    new SearchableField("Description") { IsFilterable = true },
                    new VectorSearchField("DescriptionVector", modelDimensions, vectorSearchProfileName),
                    new SearchableField("Category") { IsFilterable = true, IsSortable = true, IsFacetable = true },
                    new VectorSearchField("CategoryVector", modelDimensions, vectorSearchProfileName),
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
                SemanticSearch = new()
                {
                    Configurations =
                    {
                        new SemanticConfiguration("my-semantic-config", new()
                        {
                            TitleField = new SemanticField("HotelName"),
                            ContentFields =
                            {
                                new SemanticField("Description")
                            },
                            KeywordsFields =
                            {
                                new SemanticField("Category")
                            }
                        })
                    }
                }
            };
            await _indexClient.CreateIndexAsync(searchIndex);
            Console.WriteLine("Index created");
        }
        private async Task LoadIndexAsync()
        {
            var json = await File.ReadAllTextAsync("./Data/Hotels.json");
            var hotels = JsonSerializer.Deserialize<HotelList>(json);
            Console.WriteLine($"Loaded {hotels.value.Count} hotels");
            var hotelList = new List<Hotel2>();
            var count = 0;
            var sw = new Stopwatch();
            sw.Start();
            foreach(var hotel in hotels.value)
            {
                hotel.DescriptionVector = new ReadOnlyMemory<float>(await GetEmbeddingsAsync(hotel.Description));
                hotel.CategoryVector = new ReadOnlyMemory<float>(await GetEmbeddingsAsync(hotel.Category));
                hotelList.Add(hotel);
                Console.WriteLine($"{++count}: {sw.ElapsedMilliseconds}ms");
                sw.Restart();
                //if (++count > 2) break;
            }
            sw.Restart();
            SearchClient searchClient = new(_endpoint, _indexName, _credential);
            await searchClient.IndexDocumentsAsync(IndexDocumentsBatch.Upload(hotelList));
            Console.WriteLine($"{count} documents indexed in {sw.ElapsedMilliseconds}");
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
        private async Task ExecuteHybdridSearchAsync(string query)
        {
            SearchClient searchClient = new(_endpoint, _indexName, new AzureKeyCredential(_searchOptions.Value.Key));
            ReadOnlyMemory<float> vectorizedResult = await GetEmbeddingsAsync(query);
            SearchResults<Hotel> response = await searchClient.SearchAsync<Hotel>(
                    query,
                    new SearchOptions
                    {
                        VectorSearch = new()
                        {
                            Queries = { new VectorizedQuery(vectorizedResult) { KNearestNeighborsCount = 3, Fields = { "DescriptionVector" } } }
                        },
                    });

            int count = 0;
            Console.WriteLine($"Simple Hybrid Search Results:");
            await foreach (SearchResult<Hotel> result in response.GetResultsAsync())
            {
                count++;
                Hotel doc = result.Document;
                Console.WriteLine($"Score {result.Score}: {doc.HotelId}: {doc.HotelName}");
            }
            Console.WriteLine($"Total number of search results:{count}");
        }
        public async Task<float[]> GetEmbeddingsAsync(string input)
        {
            float[] embedding = new float[0];
            var client = new OpenAIClient(new Uri(_aiOptions.Value.Endpoint), new AzureKeyCredential(_aiOptions.Value.Key), new OpenAIClientOptions()
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
                var options = new EmbeddingsOptions(_aiOptions.Value.EmbeddingsDeployment, new List<string> { input });
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
    public class HotelList
    {
        public List<Hotel2> value { get; set; }
    }
  
    public class Hotel2
    {
        public string HotelId { get; set; }
        public string HotelName { get; set; }
        public string Description { get; set; }
        public ReadOnlyMemory<float> DescriptionVector { get; set; }
        public string Category { get; set; }
        public ReadOnlyMemory<float> CategoryVector { get; set; }
    }
}

/* Query response:
 * 
 * Query Answer:
Answer Highlights: <em>Secret Point Hotel. Boutique.</em> This classic hotel is ideally located on the main commercial artery of the city in the heart of New York. A few minutes away is Time's Square and the historic centre of the city, as well as other places of interest that make New York one of America's most attractive and cosmopolitan cities..
Answer Text: Secret Point Hotel. Boutique. This classic hotel is ideally located on the main commercial artery of the city in the heart of New York. A few minutes away is Time's Square and the historic centre of the city, as well as other places of interest that make New York one of America's most attractive and cosmopolitan cities..
1: Secret Point Hotel
Caption Highlights: Secret Point Hotel. Boutique. This classic hotel is ideally located on the main commercial artery of the city in the heart of New York. A few minutes away is Time's Square and the historic centre of the city, as well as other places of interest that make<em> New York</em> one of America's most attractive and cosmopolitan cities..
11: Regal Orb Residence Inn
Caption Text: Regal Orb Residence Inn. Extended-Stay. Your home away from home. Brand new fully equipped premium rooms, fast WiFi, full kitchen, washer & dryer, fitness center. Inner courtyard includes water features and outdoor seating. All units include fireplaces and small outdoor balconies. Pets accepted..
10: Country Home
Caption Highlights: Country Home. Extended-Stay. Save up to 50% off traditional<em> hotels.</em> Free WiFi, great location near downtown, full kitchen, washer & dryer, 24/7 support, bowling alley, fitness center and more..
Total number of search results:3


Simple Hybrid Search Results:
Score 0.03333333507180214: 1: Secret Point Hotel
Score 0.032522473484277725: 11: Regal Orb Residence Inn
Score 0.016393441706895828: 10: Country Home
Total number of search results:3

 * */


