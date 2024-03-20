using Amazon.Runtime.Internal.Util;
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

namespace MongoDB01
{
    internal class Main : IHostedService
    {
        private readonly ILogger<Main> _logger;
        private readonly AtlasMongoDBService _dbClient;
        private readonly OpenAIService _ai;
        private readonly BlobService _blob;
        public Main(
            ILogger<Main> logger, 
            OpenAIService ai, 
            AtlasMongoDBService dbClient,
            BlobService blobClient)
        {
            _logger = logger;
            _ai = ai;
            _dbClient = dbClient;
            _blob = blobClient;
        }
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            Console.WriteLine("Started");

            // await _dbClient.CreateEmbeddingAsync("sample_airbnb", "listingsAndReviews", "description");

            //await _dbClient.SearchAsync("sample_airbnb", "listingsAndReviews", "two bedrooms", 2);
            var properties = _dbClient.Get("sample_airbnb", "listingsAndReviews");
            foreach (var doc in properties)
            {
                try
                {
                    doc.Remove("embedding");
                    var prop = new
                    {
                        id = doc["_id"].ToString(),
                        name = doc["name"].AsString,
                        description = doc["description"].AsString,
                        //address = doc["address"].AsString,
                        neighborhood_overview = doc["neighborhood_overview"].AsString,
                        notes = doc["notes"].AsString,
                        transit = doc["transit"].AsString,
                    };
                    var json = JsonSerializer.Serialize(prop);
                    //File.WriteAllText($"c:\\temp\\{doc["_id"].ToString()}.json", json);
                    await _blob.SaveJsonAsync($"{doc["_id"].ToString()}.json", json);
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Exception: DownloadAsync(): {ex.Message}");
                    throw;
                }
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            Console.WriteLine("Stopped");
            return Task.CompletedTask;
        }
    }
}
