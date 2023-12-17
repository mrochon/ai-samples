using Microsoft.SemanticKernel.Connectors.AI.OpenAI;
using Microsoft.SemanticKernel.Plugins.Memory;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SK1
{
    internal class Memory
    {
        public Memory(Settings settings)
        {
            // Memory functionality is experimental
#pragma warning disable SKEXP0011, SKEXP0052
            var memoryBuilder = new MemoryBuilder();
            memoryBuilder.WithAzureOpenAITextEmbeddingGeneration(
                    settings.Model,
                    "model-id",
                    settings.Endpoint,
                    settings.Secret);

            memoryBuilder.WithMemoryStore(new VolatileMemoryStore());
            var memory = memoryBuilder.Build();
        }
    }
}
