using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Bson.Serialization;
using MongoDB.Bson;
using MongoDB.Driver;
using SharedRAG.Models;
using MongoDB.Driver.Core.Operations;
using static System.Runtime.InteropServices.JavaScript.JSType;
using System;
using System.Diagnostics;

namespace SharedRAG.Services
{
    public class AtlasMongoDBService
    {
        private ILogger<AtlasMongoDBService> _logger;
        private IOptions<MongoDBOptions> _options;
        private MongoClient _client;
        private OpenAIService _ai;

        public AtlasMongoDBService(
            ILogger<AtlasMongoDBService> logger,
            IOptions<MongoDBOptions> options,
            OpenAIService ai)
        {
            ArgumentNullException.ThrowIfNull(logger);
            ArgumentNullException.ThrowIfNull(options);
            ArgumentNullException.ThrowIfNull(ai);
            _logger = logger;
            _options = options;
            _ai = ai;
            _logger.LogTrace("Creating MongoClient");
            _client = new MongoClient(options.Value.Connection);
        }
        public IEnumerable<string> Databases
        {
            get { return _client.ListDatabaseNames().ToList(); }
        }

        public async Task CreateEmbeddingIndexAsync(string dbName, string collectionName, string indexName)
        {
            _logger.LogTrace("CreateEmbeddingIndexAsync");
            var db = _client.GetDatabase(dbName);
            var collection = db.GetCollection<BsonDocument>(collectionName);
            var list = collection.Indexes.List().ToList();
            if (collection.Indexes.List().ToList().FirstOrDefault(i => i["name"].AsString == indexName) == null)
            {
                var command = new BsonDocumentCommand<BsonDocument>(
                    new BsonDocument
                    {
                        { "createIndexes", collectionName },
                        { "indexes", new BsonArray
                        {
                                new BsonDocument
                                {
                                    { "type", "vectorSearch" },
                                    { "fields", new BsonArray
                                        {
                                            new BsonDocument
                                            {
                                                { "numDimensions", 1536 },
                                                { "path", "embedding" },
                                                { "similarity", "cosine" },
                                                { "type", "vector" }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    });

                try
                { 
                    BsonDocument result = db.RunCommand(command);
                    if (result["ok"] != 1)
                    {
                        _logger.LogError("CreateIndex failed with response: " + result.ToJson());
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Exception: CreateEmbeddingIndexAsync(): {ex.Message}");
                    throw;
                }   
            }
            _logger.LogTrace("CreateEmbeddingIndexAsync complete");
        }   

        public async Task CreateEmbeddingAsync(string dbName, string collectionName, string propertyPath)
        {
            _logger.LogTrace("CreateEmbeddingAsync");
            var collection = _client.GetDatabase(dbName).GetCollection<BsonDocument>(collectionName);
            // Return first 10 documents from a collection

            var filter = Builders<BsonDocument>.Filter.Exists("embedding", false);
            var docs = await collection.Find(filter).ToListAsync();
            _logger.LogInformation($"Creating embedding for {(docs.Count)} docs");
            //foreach (var doc in await collection.Find(filter).Limit(10).ToListAsync()) ;
            var sw = new Stopwatch();
            sw.Start();
            var count = 0;
            foreach (var doc in docs)
            {
                try
                {
                    (float[] embeddings, int tokens) = await _ai.GetEmbeddingsAsync(string.Empty, doc[propertyPath].AsString);
                    doc["embedding"] = BsonValue.Create(embeddings);
                    await collection.ReplaceOneAsync(new BsonDocument("_id", doc["_id"]), doc);
                    if ((++count % 10) == 0)
                    {
                        _logger.LogInformation($"{sw.ElapsedMilliseconds.ToString()}/10 documents");
                        sw.Reset();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Exception: CreateEmbeddingAsync(): {ex.Message}");
                    throw;
                }
            }
        }

        // Add Search: https://www.mongodb.com/docs/atlas/atlas-vector-search/vector-search-tutorial/

        public async Task AddDocumentWithVectorAsync(string dbName, string collectionName, string json, string propertyPath)
        {
            try
            {
                IEnumerable<BsonDocument> documents = BsonSerializer.Deserialize<IEnumerable<BsonDocument>>(json);
                foreach (var document in documents)
                {
                    (float[] embeddings, int tokens) = await _ai.GetEmbeddingsAsync(string.Empty, document[propertyPath].AsString);
                    document["vector"] = BsonValue.Create(embeddings);
                    await _client.GetDatabase(dbName).GetCollection<BsonDocument>(collectionName).InsertOneAsync(document);
                }
            }
            catch (MongoException ex)
            {
                _logger.LogError($"Exception: ImportJsonAsync(): {ex.Message}");
                throw;
            }
        }
        //public async Task<Customer> UpsertCustomerAsync(Customer customer)
        //{
        //    try
        //    {
        //        string sObject = RemoveVectorAndSerialize(customer);
        //        (customer.vector, int tokens) = await _openAiService.GetEmbeddingsAsync(string.Empty, sObject);
        //        await _customers.ReplaceOneAsync(
        //            filter: Builders<Customer>.Filter.Eq("customerId", customer.customerId)
        //                  & Builders<Customer>.Filter.Eq("_id", customer.id),
        //            options: new ReplaceOptions { IsUpsert = true },
        //            replacement: customer);

        //    }
        //    catch (MongoException ex)
        //    {
        //        _logger.LogError($"Exception: UpsertCustomerAsync(): {ex.Message}");
        //        throw;
        //    }
        //    return customer;
        //}
        //private string RemoveVectorAndSerialize(object o)
        //{
        //    string sObject = string.Empty;
        //    try
        //    {
        //        JObject obj = JObject.FromObject(o);
        //        obj.Remove("vector");
        //        sObject = obj.ToString();
        //    }
        //    catch { }
        //    return sObject;
        //}
    }
}
