using Amazon.Runtime.Internal.Util;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Embeddings;
using SKApps.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

#pragma warning disable SKEXP0001
namespace SKApps.Plugins
{
    /// <summary>
    /// Azure AI Search SK Plugin.
    /// It uses <see cref="ITextEmbeddingGenerationService"/> to convert string query to vector.
    /// It uses <see cref="IAzureAISearchService"/> to perform a request to Azure AI Search.
    /// </summary>
    public sealed class AzureAISearchPlugin
    {
        ILogger<AzureAISearchPlugin> _logger;
        private readonly OpenAIService _textEmbeddingGenerationService;
        private readonly ISearchService _searchService;

        public AzureAISearchPlugin(
            ILogger<AzureAISearchPlugin> logger,
            OpenAIService textEmbeddingGenerationService,
            ISearchService searchService)
        {
            _logger = logger;
            this._textEmbeddingGenerationService = textEmbeddingGenerationService;
            this._searchService = searchService;
        }

        [KernelFunction("AISearch")]
        public async Task<string> SearchAsync(
            string query,
            string collection,
            List<string>? searchFields = null,
            string filter = "",
            CancellationToken cancellationToken = default)
        {
            _logger.LogTrace($"SearchAsync using: {collection}");
            // Convert string query to vector
            //ReadOnlyMemory<float> embedding = await this._textEmbeddingGenerationService.GenerateEmbeddingAsync(query, cancellationToken: cancellationToken);
            _logger.LogTrace($"SearchAsync: Generating embeddings for: {query}");
            (float[] embedding, int tokens) = await this._textEmbeddingGenerationService.GetEmbeddingsAsync(Guid.NewGuid().ToString(), query);
            _logger.LogTrace($"SearchAsync: Generated embeddings. Used {query} tokens.");
            // Perform search
            var docs = await this._searchService.SearchAsync(collection, embedding, searchFields, filter, cancellationToken);
            //var resp = docs.Aggregate(new StringBuilder(), (sb, doc) => sb.AppendLine(doc.ToString())).ToString();
            _logger.LogTrace($"SearchAsync: result: {docs}");
            return docs;
        }
    }
}
