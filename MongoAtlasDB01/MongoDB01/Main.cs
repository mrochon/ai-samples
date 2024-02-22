using Microsoft.Extensions.Hosting;
using MongoDB.Bson;
using SharedRAG.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MongoDB01
{
    internal class Main : IHostedService
    {
        private readonly AtlasMongoDBService _dbClient;
        private readonly OpenAIService _ai;
        public Main(OpenAIService ai, AtlasMongoDBService dbClient)
        {
            _ai = ai;
            _dbClient = dbClient;
        }
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            Console.WriteLine("Started");

            await _dbClient.CreateEmbeddingAsync("sample_airbnb", "listingsAndReviews", "description");
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            Console.WriteLine("Stopped");
            return Task.CompletedTask;
        }
    }
}
