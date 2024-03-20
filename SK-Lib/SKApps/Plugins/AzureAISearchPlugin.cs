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
        private readonly OpenAIService _textEmbeddingGenerationService;
        private readonly ISearchService _searchService;

        public AzureAISearchPlugin(
            OpenAIService textEmbeddingGenerationService,
            ISearchService searchService)
        {
            this._textEmbeddingGenerationService = textEmbeddingGenerationService;
            this._searchService = searchService;
        }

        [KernelFunction("Search")]
        public async Task<string> SearchAsync(
            string query,
            string collection,
            List<string>? searchFields = null,
            CancellationToken cancellationToken = default)
        {
            // Convert string query to vector
            //ReadOnlyMemory<float> embedding = await this._textEmbeddingGenerationService.GenerateEmbeddingAsync(query, cancellationToken: cancellationToken);
            (float[] embedding, int tokens) = await this._textEmbeddingGenerationService.GetEmbeddingsAsync(Guid.NewGuid().ToString(), query);

            // Perform search
            var docs = await this._searchService.SearchAsync(collection, embedding, searchFields, cancellationToken);
            return docs.Aggregate(new StringBuilder(), (sb, doc) => sb.AppendLine(doc.ToString())).ToString();
        }
    }
}
