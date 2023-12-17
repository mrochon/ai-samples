using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.AI.OpenAI;
using Microsoft.SemanticKernel.Memory;
using Microsoft.SemanticKernel.Plugins.Memory;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace SK1
{
    internal static class KernelExtensions
    {
        internal static void Summarize(this Kernel kernel, params string[] text)
        {
            var prompt = @"{{$input}}

One line, with the fewest words.";  // was 'One line TLDR with fewest words' TLDR= Too Long Didn't Read

            var summarize = kernel.CreateFunctionFromPrompt(prompt, executionSettings: new OpenAIPromptExecutionSettings { MaxTokens = 100 });

            foreach(var t in text)
            {
                Console.WriteLine(kernel.InvokeAsync(summarize, new KernelArguments(t)).Result);
            }
        }
        public static ISemanticTextMemory WithMemory(this Kernel kernel, Settings settings)
        {
            var memoryBuilder = new MemoryBuilder();
            memoryBuilder.WithAzureOpenAITextEmbeddingGeneration(
                    "mrtextembeddingada002", // settings.Model,
                    "text-embedding-ada-002",
                    settings.Endpoint,
                    settings.Secret);
            memoryBuilder.WithMemoryStore(new VolatileMemoryStore());
            return memoryBuilder.Build();
        }
    }
}
