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

// https://github.com/Azure/azure-sdk-for-net/blob/Azure.Search.Documents_11.5.1/sdk/search/Azure.Search.Documents/samples/Sample02_Service.md

namespace SearchApps.Apps
{
    public class Service : IHostedService
    {
        private readonly ILogger<Service> _logger;
        private readonly IOptions<AzureAISearchOptions> _searchOptions;
        private readonly IOptions<BlobOptions> _blobOptions;
        private readonly SearchIndexClient _indexClient;
        private readonly Uri _endpoint;
        private readonly AzureKeyCredential _credential;

        private readonly string _indexName;
        private readonly string _dataSourceConnectionName;
        public Service(
            ILogger<Service> logger,
            IOptions<AzureAISearchOptions> searchOptions,
            IOptions<BlobOptions> blobOptions)
        {
            _logger = logger;
            _searchOptions = searchOptions;
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

            // Get and report the Search Service statistics
            Response<SearchServiceStatistics> stats = await _indexClient.GetServiceStatisticsAsync();
            Console.WriteLine($"You are using {stats.Value.Counters.IndexCounter.Usage} of {stats.Value.Counters.IndexCounter.Quota} indexes.");

            //await CreateSynonymMapAsync();

            //await CreateIndexAsync();

            await CreateDataSourceConnectionAsync();

            var indexerClient = CreateIndexerClient();

            await CreateIndexerAsync(indexerClient);

            await QueryAsync();

            _logger.LogTrace($"{this.GetType().Name} Done");
            await StopAsync(cancellationToken);
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogTrace($"{this.GetType().Name} Stopping");
            return Task.CompletedTask;
        }

        private async Task CreateSynonymMapAsync()
        {
            // Create a synonym map from a file containing country names and abbreviations
            // using the Solr format with entry on a new line using \n, for example:
            // United States of America,US,USA\n
            string synonymMapName = "countries";
            string synonymMapPath = "./data/countries.txt";

            SynonymMap synonyms;
            using (StreamReader file = File.OpenText(synonymMapPath))
            {
                synonyms = new SynonymMap(synonymMapName, file);
            }
            await _indexClient.DeleteSynonymMapAsync(synonyms);
            await _indexClient.CreateSynonymMapAsync(synonyms);
            Console.WriteLine($"Created synonym map for {synonymMapName}");
        }

        private async Task CreateIndexAsync()
        {
            Console.WriteLine("Creating index");
            var countryField = new SearchableField("Country") { IsFilterable = true, IsSortable = true, IsFacetable = true };
            countryField.SynonymMapNames.Add("countries");
            SearchIndex index = new SearchIndex(_indexName)
            {
                Fields =
                {
                    new SimpleField("HotelId", SearchFieldDataType.String) { IsKey = true, IsFilterable = true, IsSortable = true },
                    new SearchableField("HotelName") { IsFilterable = true, IsSortable = true },
                    new SearchableField("Description") { AnalyzerName = LexicalAnalyzerName.EnLucene },
                    new SearchableField("DescriptionFr") { AnalyzerName = LexicalAnalyzerName.FrLucene },
                    new SearchableField("Tags", collection: true) { IsFilterable = true, IsFacetable = true },
                    new ComplexField("Address")
                    {
                        Fields =
                        {
                            new SearchableField("StreetAddress"),
                            new SearchableField("City") { IsFilterable = true, IsSortable = true, IsFacetable = true },
                            new SearchableField("StateProvince") { IsFilterable = true, IsSortable = true, IsFacetable = true },
                            countryField,
                            new SearchableField("PostalCode") { IsFilterable = true, IsSortable = true, IsFacetable = true }
                        }
                    }
                }
            };

            await _indexClient.CreateIndexAsync(index);
        }

        private async Task CreateDataSourceConnectionAsync()
        {
            Console.WriteLine("Creating data sourceconnection");
            SearchIndexerClient indexerClient = new SearchIndexerClient(new Uri(_searchOptions.Value.Endpoint), new AzureKeyCredential(_searchOptions.Value.Key));
            SearchIndexerDataSourceConnection dataSourceConnection = new SearchIndexerDataSourceConnection(
                _dataSourceConnectionName,
                SearchIndexerDataSourceType.AzureBlob,
                _blobOptions.Value.SASUrl,
                new SearchIndexerDataContainer("general"));

            await indexerClient.CreateDataSourceConnectionAsync(dataSourceConnection);
        }

        private SearchIndexerClient CreateIndexerClient()
        {
            // Create SearchIndexerClient options
            SearchClientOptions options = new SearchClientOptions()
            {
                Transport = new HttpClientTransport(new HttpClient()
                {
                    // Increase timeout for each request to 5 minutes
                    Timeout = TimeSpan.FromMinutes(5)
                })
            };

            // Increase retry attempts to 6
            options.Retry.MaxRetries = 6;

            // Create a new SearchIndexerClient with options
            return new SearchIndexerClient(_endpoint, _credential, options);
        }

        private async Task CreateIndexerAsync(SearchIndexerClient indexerClient)
        {
            // Translate English descriptions to French.
            // See https://docs.microsoft.com/azure/search/cognitive-search-skill-text-translation for details of the Text Translation skill.
            TextTranslationSkill translationSkill = new TextTranslationSkill(
                inputs: new[]
                {
                    new InputFieldMappingEntry("text") { Source = "/document/Description" }
                },
                outputs: new[]
                {
                    new OutputFieldMappingEntry("translatedText") { TargetName = "descriptionFrTranslated" }
                },
                TextTranslationSkillLanguage.Fr)
            {
                Name = "descriptionFrTranslation",
                Context = "/document",
                DefaultFromLanguageCode = TextTranslationSkillLanguage.En
            };

            // Use the human-translated French description if available; otherwise, use the translated description.
            // See https://docs.microsoft.com/azure/search/cognitive-search-skill-conditional for details of the Conditional skill.
            ConditionalSkill conditionalSkill = new ConditionalSkill(
                inputs: new[]
                {
                    new InputFieldMappingEntry("condition") { Source = "= $(/document/DescriptionFr) == null" },
                    new InputFieldMappingEntry("whenTrue") { Source = "/document/descriptionFrTranslated" },
                    new InputFieldMappingEntry("whenFalse") { Source = "/document/DescriptionFr" }
                },
                outputs: new[]
                {
                    new OutputFieldMappingEntry("output") { TargetName = "descriptionFrFinal"}
                })
                {
                    Name = "descriptionFrConditional",
                    Context = "/document",
                };

            // Create a SearchIndexerSkillset that processes those skills in the order given below.
            string skillsetName = "translations";
            SearchIndexerSkillset skillset = new SearchIndexerSkillset(
                skillsetName,
                new SearchIndexerSkill[] { translationSkill, conditionalSkill })
                {
                    CognitiveServicesAccount = new CognitiveServicesAccountKey(_searchOptions.Value.AdminKey),
                    KnowledgeStore = new KnowledgeStore(_searchOptions.Value.AdminKey,
                        new List<KnowledgeStoreProjection>()),
                };

            await indexerClient.CreateSkillsetAsync(skillset);

            string indexerName = "hotels";
            SearchIndexer indexer = new SearchIndexer(
                indexerName,
                _dataSourceConnectionName,
                _indexName)
            {
                // We only want to index fields defined in our index, excluding descriptionFr if defined.
                FieldMappings =
                {
                    new FieldMapping("HotelId"),
                    new FieldMapping("HotelName"),
                    new FieldMapping("Description"),
                    new FieldMapping("Tags"),
                    new FieldMapping("Address")
                },
                            OutputFieldMappings =
                {
                    new FieldMapping("/document/descriptionFrFinal") { TargetFieldName = "DescriptionFr" }
                },
                Parameters = new IndexingParameters
                {
                    // Tell the indexer to parse each blob as a separate JSON document.
                    IndexingParametersConfiguration = new IndexingParametersConfiguration
                    {
                        ParsingMode = BlobIndexerParsingMode.Json
                    }
                },
                SkillsetName = skillsetName
            };

            // Create the indexer which, upon successful creation, also runs the indexer.
            await indexerClient.CreateIndexerAsync(indexer);
        }

        private async Task QueryAsync()
        {
            // Get a SearchClient from the SearchIndexClient to share its pipeline.
            SearchClient searchClient = _indexClient.GetSearchClient(_indexName);

            // Query for hotels with an ocean view.
            SearchResults<Hotel> results = await searchClient.SearchAsync<Hotel>("ocean view");
            await foreach (SearchResult<Hotel> result in results.GetResultsAsync())
            {
                Hotel hotel = result.Document;

                Console.WriteLine($"{hotel.HotelName} ({hotel.HotelId})");
                Console.WriteLine($"  Description (English): {hotel.Description}");
                Console.WriteLine($"  Description (French):  {hotel.DescriptionFr}");
            }
        }
    }

    public class Hotel
    {
        public string? HotelId { get; set; }
        public string? HotelName { get; set; }
        public string? Description { get; set; }
        public string? DescriptionFr { get; set; }
    }
}
