using Azure;
using Azure.AI.OpenAI;
using Azure.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SharedRAG.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharedRAG.Services
{
    public class OpenAIService
    {
        private ILogger<OpenAIService> _logger;
        private IOptions<OpenAIOptions> _options;
        private readonly OpenAIClient _client;
        public OpenAIService(
            ILogger<OpenAIService> logger,
            IOptions<OpenAIOptions> options)
        {
            ArgumentNullException.ThrowIfNull(logger);
            ArgumentNullException.ThrowIfNull(options);
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
            _client = new(new Uri(_options.Value.Endpoint), new AzureKeyCredential(_options.Value.Key), aiOptions);
        }

        public async Task<(float[] vectors, int promptTokens)> GetEmbeddingsAsync(string sessionId, string input)
        {
            float[] embedding = new float[0];
            int responseTokens = 0;
            try
            {
                EmbeddingsOptions options = new EmbeddingsOptions(_options.Value.EmbeddingsDeployment, new List<string> { input })
                {
                    User = sessionId
                };
                var response = await _client.GetEmbeddingsAsync(options);
                Embeddings embeddings = response.Value;
                responseTokens = embeddings.Usage.TotalTokens;
                embedding = embeddings.Data[0].Embedding.ToArray();
                _logger.LogTrace($"Got embeddings: used {responseTokens} tokens for: {input.Substring(0, 20)}...");
                return (embedding, responseTokens);
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
