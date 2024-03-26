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
using Microsoft.Extensions.Configuration;
using System.Text.Json.Serialization;

// https://learn.microsoft.com/en-us/azure/search/cognitive-search-tutorial-blob-dotnet
namespace SearchApps.Apps
{
    public class SkillsPipeline : IHostedService
    {
        private readonly ILogger<SkillsPipeline> _logger;
        private readonly IOptions<AzureAISearchOptions> _searchOptions;
        private readonly IOptions<BlobOptions> _blobOptions;
        private readonly SearchIndexClient _indexClient;
        private readonly SearchIndexerClient _indexerClient;
        SearchIndexerSkillset? _skillset;
        private readonly Uri _endpoint;
        private readonly AzureKeyCredential _credential;

        private readonly string _indexName;
        private readonly string _indexerName;
        private readonly string _dataSourceName;
        public SkillsPipeline(
            ILogger<SkillsPipeline> logger,
            IOptions<AzureAISearchOptions> searchOptions,
            IOptions<BlobOptions> blobOptions)
        {
            _logger = logger;
            _searchOptions = searchOptions;
            _blobOptions = blobOptions;
            _blobOptions.Value.ConnectionString = String.Format($"DefaultEndpointsProtocol=https;AccountName={_blobOptions.Value.AccountName};AccountKey={_blobOptions.Value.Key};EndpointSuffix=core.windows.net");
            ArgumentNullException.ThrowIfNull(logger);
            ArgumentNullException.ThrowIfNull(_searchOptions.Value.Key);
            ArgumentNullException.ThrowIfNull(_searchOptions.Value.Endpoint);
            ArgumentNullException.ThrowIfNull(_searchOptions.Value.AdminKey);

            _endpoint = new Uri(_searchOptions.Value.Endpoint);
            _credential = new AzureKeyCredential(_searchOptions.Value.AdminKey);

            _indexClient = new SearchIndexClient(_endpoint, _credential);
            _indexerClient = new SearchIndexerClient(_endpoint, _credential);

            _indexName = "hotelsreview";
            _indexerName = "hotelreviewsfromcvs";
            _dataSourceName = "hotelreviewsincsv";
        }
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogTrace($"{this.GetType().Name} Started");

            Console.WriteLine("Creating or updating the data source...");
            var dataSource = CreateOrUpdateDataSource();

            // Create the skillset
            Console.WriteLine("Creating or updating the skillset...");
            var skills = new List<SearchIndexerSkill>()
            {
                CreateDocumentExtractionSkill(),
                CreateLanguageDetectionSkill(),
                CreateSplitSkill(),
                CreateKeyPhraseExtractionSkill(),
            };
            // Obsoleted, what now? https://learn.microsoft.com/en-us/azure/search/cognitive-search-skill-deprecated
            //skills.Add(CreateEntityRecognitionSkill());
            _skillset = CreateOrUpdateSkillSet(skills);

            // Create the index
            Console.WriteLine("Creating the index...");
            var index = CreateIndex();

            var createIndexerTask = Task.Factory.StartNew(() => CreateIndexer());
            // Check indexer overall status
            Console.WriteLine("Check the indexer overall status...");
            CheckIndexerOverallStatus();

            Task.WaitAll(createIndexerTask);

            _logger.LogTrace($"{this.GetType().Name} Done");
            await StopAsync(cancellationToken);
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogTrace($"{this.GetType().Name} Stopping");
            return Task.CompletedTask;
        }

        private SearchIndexerDataSourceConnection CreateOrUpdateDataSource()
        {
            SearchIndexerDataSourceConnection dataSource = new SearchIndexerDataSourceConnection(
                name: _dataSourceName,
                type: SearchIndexerDataSourceType.AzureBlob,
                connectionString: _blobOptions.Value.ConnectionString,
                container: new SearchIndexerDataContainer(_blobOptions.Value.ReviewsContainer))
            {
                Description = " files to demonstrate Azure AI Search capabilities."
            };
            try
            {
                _indexerClient.CreateOrUpdateDataSourceConnection(dataSource);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to create or update the data source\n Exception message: {0}\n", ex.Message);
                throw;
            }
            return dataSource;
        }
        private SearchIndexerSkillset CreateOrUpdateSkillSet(IList<SearchIndexerSkill> skills)
        {
            SearchIndexerSkillset skillset = new SearchIndexerSkillset("indexreviewcsvskillset", skills)
            {
                // Azure AI services was formerly known as Cognitive Services.
                // The APIs still use the old name, so we need to create a CognitiveServicesAccountKey object.
                Description = " skillset",
                CognitiveServicesAccount = new CognitiveServicesAccountKey(_searchOptions.Value.SkillsetKey)
            };

            // Create the skillset in your search service.
            // The skillset does not need to be deleted if it was already created
            // since we are using the CreateOrUpdate method
            try
            {
                _indexerClient.CreateOrUpdateSkillset(skillset);
            }
            catch (RequestFailedException ex)
            {
                Console.WriteLine("Failed to create the skillset\n Exception message: {0}\n", ex.Message);
                throw;
            }
            return skillset;
        }
        private static DocumentExtractionSkill CreateDocumentExtractionSkill()
        {
            List<InputFieldMappingEntry> inputMappings = new List<InputFieldMappingEntry>();
            inputMappings.Add(new InputFieldMappingEntry("file_data")
            {
                Source = "/document/file_data"
            });

            List<OutputFieldMappingEntry> outputMappings = new List<OutputFieldMappingEntry>();
            outputMappings.Add(new OutputFieldMappingEntry("content")
            {
                TargetName = "extracted_content"
            });
            var documentExtractionSkill = new DocumentExtractionSkill(inputMappings, outputMappings)
            {
                Description = "Break multi-line csv file into one document per line",
                Context = "/document",
                ParsingMode = BlobIndexerParsingMode.Default
            };
            //documentExtractionSkill.Configuration.Add("dataToExtract", "contentAndMetadata");
            //documentExtractionSkill.Configuration.Add("delimitedTextDelimiter", ",");
            //documentExtractionSkill.Configuration.Add("delimitedTextHeaders", "name,postalCode,province,reviews_date,reviews_dateAdded,reviews_rating,reviews_text,reviews_title");
            return documentExtractionSkill;
        }
        private static LanguageDetectionSkill CreateLanguageDetectionSkill()
        {
            List<InputFieldMappingEntry> inputMappings = new List<InputFieldMappingEntry>();
            inputMappings.Add(new InputFieldMappingEntry("text")
            {
                Source = "/document/merged_text"
            });

            List<OutputFieldMappingEntry> outputMappings = new List<OutputFieldMappingEntry>();
            outputMappings.Add(new OutputFieldMappingEntry("languageCode")
            {
                TargetName = "languageCode"
            });

            LanguageDetectionSkill languageDetectionSkill = new LanguageDetectionSkill(inputMappings, outputMappings)
            {
                Description = "Detect the language used in the document",
                Context = "/document"
            };

            return languageDetectionSkill;
        }
        private static SplitSkill CreateSplitSkill()
        {
            List<InputFieldMappingEntry> inputMappings = new List<InputFieldMappingEntry>();
            inputMappings.Add(new InputFieldMappingEntry("text")
            {
                Source = "/document/merged_text"
            });
            inputMappings.Add(new InputFieldMappingEntry("languageCode")
            {
                Source = "/document/languageCode"
            });

            List<OutputFieldMappingEntry> outputMappings = new List<OutputFieldMappingEntry>();
            outputMappings.Add(new OutputFieldMappingEntry("textItems")
            {
                TargetName = "pages",
            });

            SplitSkill splitSkill = new SplitSkill(inputMappings, outputMappings)
            {
                Description = "Split content into pages",
                Context = "/document",
                TextSplitMode = TextSplitMode.Pages,
                MaximumPageLength = 4000,
                DefaultLanguageCode = SplitSkillLanguage.En
            };

            return splitSkill;
        }
        private static EntityRecognitionSkill CreateEntityRecognitionSkill()
        {
            List<InputFieldMappingEntry> inputMappings = new List<InputFieldMappingEntry>();
            inputMappings.Add(new InputFieldMappingEntry("text")
            {
                Source = "/document/pages/*"
            });

            List<OutputFieldMappingEntry> outputMappings = new List<OutputFieldMappingEntry>();
            outputMappings.Add(new OutputFieldMappingEntry("organizations")
            {
                TargetName = "organizations"
            });

            EntityRecognitionSkill entityRecognitionSkill = new EntityRecognitionSkill(inputMappings, outputMappings)
            {
                Description = "Recognize organizations",
                Context = "/document/pages/*",
                DefaultLanguageCode = EntityRecognitionSkillLanguage.En
            };
            entityRecognitionSkill.Categories.Add(EntityCategory.Organization);

            return entityRecognitionSkill;
        }
        private static KeyPhraseExtractionSkill CreateKeyPhraseExtractionSkill()
        {
            List<InputFieldMappingEntry> inputMappings = new List<InputFieldMappingEntry>();
            inputMappings.Add(new InputFieldMappingEntry("text")
            {
                Source = "/document/pages/*"
            });
            inputMappings.Add(new InputFieldMappingEntry("languageCode")
            {
                Source = "/document/languageCode"
            });

            List<OutputFieldMappingEntry> outputMappings = new List<OutputFieldMappingEntry>();
            outputMappings.Add(new OutputFieldMappingEntry("keyPhrases")
            {
                TargetName = "keyPhrases"
            });

            KeyPhraseExtractionSkill keyPhraseExtractionSkill = new KeyPhraseExtractionSkill(inputMappings, outputMappings)
            {
                Description = "Extract the key phrases",
                Context = "/document/pages/*",
                DefaultLanguageCode = KeyPhraseExtractionSkillLanguage.En
            };

            return keyPhraseExtractionSkill;
        }

        private SearchIndex CreateIndex()
        {
            FieldBuilder builder = new FieldBuilder();
            var index = new SearchIndex(_indexName)
            {
                Fields = builder.Build(typeof(Index))
            };

            try
            {
                _indexClient.GetIndex(index.Name);
                _indexClient.DeleteIndex(index.Name);
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                //if the specified index not exist, 404 will be thrown.
            }

            try
            {
                _indexClient.CreateIndex(index);
            }
            catch (RequestFailedException ex)
            {
                Console.WriteLine("Failed to create the index\n Exception message: {0}\n", ex.Message);
                throw;
            }
            return index;
        }

        private SearchIndexer CreateIndexer()
        {
            Console.WriteLine("Creating the indexer and executing the pipeline...");
            var indexingParameters = new IndexingParameters()
            {
                MaxFailedItems = -1,
                MaxFailedItemsPerBatch = -1,
            };
            indexingParameters.Configuration.Add("dataToExtract", "contentAndMetadata");
            indexingParameters.Configuration.Add("imageAction", "generateNormalizedImages");

            var indexer = new SearchIndexer(_indexerName, _dataSourceName, _indexName)
            {
                Description = _indexerName,
                SkillsetName = _skillset.Name,
                Parameters = indexingParameters
            };

            var mappingFunction = new FieldMappingFunction("base64Encode");
            mappingFunction.Parameters.Add("useHttpServerUtilityUrlTokenEncode", true);

            indexer.FieldMappings.Add(new FieldMapping("metadata_storage_path")
            {
                TargetFieldName = "id",
                MappingFunction = mappingFunction

            });
            indexer.FieldMappings.Add(new FieldMapping("content")
            {
                TargetFieldName = "content"
            });

            indexer.OutputFieldMappings.Add(new FieldMapping("/document/pages/*/organizations/*")
            {
                TargetFieldName = "organizations"
            });
            indexer.OutputFieldMappings.Add(new FieldMapping("/document/pages/*/keyPhrases/*")
            {
                TargetFieldName = "keyPhrases"
            });
            indexer.OutputFieldMappings.Add(new FieldMapping("/document/languageCode")
            {
                TargetFieldName = "languageCode"
            });

            try
            {
                _indexerClient.GetIndexer(indexer.Name);
                _indexerClient.DeleteIndexer(indexer.Name);
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                //if the specified indexer not exist, 404 will be thrown.
            }

            try
            {
                _indexerClient.CreateIndexer(indexer);
            }
            catch (RequestFailedException ex)
            {
                Console.WriteLine("Failed to create the indexer\n Exception message: {0}\n", ex.Message);
                throw;
            }
            return indexer;
        }

        private void CheckIndexerOverallStatus()
        {
            // Repeat every 5s
            do
            {

                try
                {
                    var indexerExecutionInfo = _indexerClient.GetIndexerStatus(_indexerName);

                    switch (indexerExecutionInfo.Value.Status)
                    {
                        case IndexerStatus.Error:
                            throw new Exception("Indexer has error status. Check the Azure Portal to further understand the error.");
                        case IndexerStatus.Running:
                            Console.WriteLine("Indexer is running");
                            break;
                        case IndexerStatus.Unknown:
                            Console.WriteLine("Indexer status is unknown");
                            break;
                        default:
                            Console.WriteLine("No indexer information");
                            break;
                    }
                    Task.Delay(5000).Wait();
                }
                catch (RequestFailedException ex)
                {
                    Console.WriteLine("Failed to get indexer overall status\n Exception message: {0}\n", ex.Message);
                    throw;
                }
            } while (true);
        }

    }

    // The SerializePropertyNamesAsCamelCase is currently unsupported as of this writing. 
    // Replace it with JsonPropertyName
    public class Index
    {
        [SearchableField(IsSortable = true, IsKey = true)]
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [SearchableField]
        [JsonPropertyName("content")]
        public string Content { get; set; }

        [SearchableField]
        [JsonPropertyName("languageCode")]
        public string LanguageCode { get; set; }

        [SearchableField]
        [JsonPropertyName("keyPhrases")]
        public string[] KeyPhrases { get; set; }

        [SearchableField]
        [JsonPropertyName("organizations")]
        public string[] Organizations { get; set; }
    }
}
