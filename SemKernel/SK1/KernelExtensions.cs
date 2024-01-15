using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Memory;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.Planning.Handlebars;


namespace SK1
{
    internal static class KernelExtensions
    {
        public static Dictionary<string, KernelPlugin> LoadPlugins(this Kernel kernel, params string[] pluginNames)
        {
            Dictionary<string, KernelPlugin> plugins = new Dictionary<string, KernelPlugin>();
            var pluginPath = Path.Combine(System.IO.Directory.GetCurrentDirectory(), "..", "..", "..", "plugins");
            foreach (var plugin in pluginNames)
            {
                plugins.Add(plugin, kernel.CreatePluginFromPromptDirectory(Path.Combine(pluginPath, plugin)));
            }
            return plugins;
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

        public static async Task<HandlebarsPlan> ShowPlanAsync(this Kernel kernel, string prompt)
        {
            var planner = new HandlebarsPlanner();
            var plan = await planner.CreatePlanAsync(kernel, prompt);
            plan.I
            Console.WriteLine("Original plan:\n");
            Console.WriteLine(plan);
            return plan;
        }
        /*
{{!-- Step 1: Initialize an array of date ideas --}}
{{set "dateIdeas" (array)}}

{{!-- Step 2: Add date ideas to the array --}}
{{set "dateIdeas" (concat (get "dateIdeas") "A romantic picnic in the park,")}}
{{set "dateIdeas" (concat (get "dateIdeas") "A candlelit dinner for two,")}}
{{set "dateIdeas" (concat (get "dateIdeas") "A moonlit walk on the beach,")}}
{{set "dateIdeas" (concat (get "dateIdeas") "A cozy movie night at home,")}}
{{set "dateIdeas" (concat (get "dateIdeas") "A couples' spa day,")}}

{{!-- Step 3: Create the poem using the date ideas --}}
{{set "poem" (concat "On Valentine's day, let's have some fun," (get "dateIdeas" 0) "\n" (get "dateIdeas" 1) "\n" (get "dateIdeas" 2) "\n" (get "dateIdeas" 3) "\n" (get "dateIdeas" 4))}}

{{!-- Step 4: Print the poem to the screen --}}
{{json (get "poem")}}
*/

        public static async Task<int> ChatAsync(this Kernel kernel)
        {
            const string prompt = @"
ChatBot can have a conversation with you about any topic.
It can give explicit instructions or say 'I don't know' if it does not have an answer.

{{$history}}
User: {{$userInput}}
ChatBot:";
            var kernelFunction = kernel.CreateFunctionFromPrompt(
                prompt,
                new OpenAIPromptExecutionSettings
                {
                    MaxTokens = 2000,
                    Temperature = 0.7,
                    TopP = 0.5
                });
            var kernelArgs = new KernelArguments
            {
                ["history"] = ""
            };
            Console.WriteLine("Start asking me questions...");
            var responses = 0;
            do
            {
                var input = Console.ReadLine();
                if (input!.StartsWith("q", StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }
                ++responses;
                kernelArgs["userInput"] = input;
                var answer = await kernelFunction.InvokeAsync(kernel, kernelArgs);
                var result = $"\nUser: {input}\nMelody: {answer}\n";
                kernelArgs["history"] += result;
                Console.WriteLine(result);
            } while (true);
            return responses;
        }
    }
}
