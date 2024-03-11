using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Memory;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.Planning.Handlebars;
using Microsoft.Extensions.Options;
using Models;
using System;
using Microsoft.SemanticKernel.Plugins.Memory;

namespace SQLCopilot.Services
{
    internal class CopilotService: IHostedService
    {
        private readonly ILogger<CopilotService> _logger;
        private readonly Kernel _kernel;
#pragma warning disable SKEXP0003
        private readonly ISemanticTextMemory _memory;
        public CopilotService(
            ILogger<CopilotService> logger,
            IOptions<OpenAIOptions> options)
        {
            _logger = logger;
            ArgumentNullException.ThrowIfNull(options, nameof(options));
            ArgumentNullException.ThrowIfNull(options.Value, nameof(options.Value));
            ArgumentNullException.ThrowIfNull(options.Value.Endpoint, nameof(options.Value.Endpoint));
            ArgumentNullException.ThrowIfNull(options.Value.Key, nameof(options.Value.Key));
            ArgumentNullException.ThrowIfNull(options.Value.ChatDeployment, nameof(options.Value.ChatDeployment));
            ArgumentNullException.ThrowIfNull(options.Value.EmbeddingDeployment, nameof(options.Value.EmbeddingDeployment));
            _kernel = Kernel.CreateBuilder()
                .AddAzureOpenAIChatCompletion(
                     options.Value.ChatDeployment,   // deployment name
                     options.Value.Endpoint!, // Azure OpenAI Endpoint
                     options.Value.Key!)      // Azure OpenAI Key
                .Build();
            var memoryBuilder = new MemoryBuilder();
#pragma warning disable SKEXP0052, SKEXP0011
            memoryBuilder.WithAzureOpenAITextEmbeddingGeneration(
                     options.Value.EmbeddingDeployment,
                     options.Value.Endpoint, // Azure OpenAI Endpoint
                     options.Value.Key)      // Azure OpenAI Key
                .WithMemoryStore(new VolatileMemoryStore());
            _memory = memoryBuilder.Build();
        }
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            Console.WriteLine("CopilotService Started");

            await GenerateUsingSchemaAsync();

            return;

            var pluginPath = Path.Combine(System.IO.Directory.GetCurrentDirectory(), "..", "..", "..", "Functions");
            var plugin = _kernel.CreatePluginFromPromptDirectory(pluginPath);
            _kernel.Plugins.Add(plugin);

            var ask = "Which orders are over $100?";
            // var ask = "how many people are there in human resource department?";
            var resp = await _kernel.InvokeAsync("Functions", "SchemaDef", new()
            {
                { "query", ask }
            });
            Console.WriteLine(resp);

//#pragma warning disable SKEXP0060
//            var planner = new HandlebarsPlanner() { };
//            var plan = await planner.CreatePlanAsync(_kernel, ask);
//            Console.WriteLine("Original plan:\n");
//            Console.WriteLine(plan);
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            Console.WriteLine("CopilotService Stopped");
            return Task.CompletedTask;
        }

        private async Task GenerateUsingSchemaAsync()
        {
            const string MemoryCollectionName = "schema";

            await _memory.SaveInformationAsync(MemoryCollectionName, id: "orders", text: "orders(id, amount, date, customerId)");
            await _memory.SaveInformationAsync(MemoryCollectionName, id: "customers", text: "customers(id, name, zip, city)");

#pragma warning disable SKEXP0052
            // TextMemoryPlugin provides the "recall" function
            _kernel.ImportPluginFromObject(new TextMemoryPlugin(_memory));
            const string skPrompt = @"
Use {{recall $query}} and SQL syntax definitions to create an SQL command to retrieve data 
specified by the {{$userInput}}. Include any search conditions specified in the {{$userInput}}.
When returning an SQL command, prefix the response with SQL: and a space.
When returning a chat response, prefix the response with ChatBot: and a space.

User: {{$userInput}}
ChatBot: ";
            var chatFunction = _kernel.CreateFunctionFromPrompt(skPrompt, new OpenAIPromptExecutionSettings { MaxTokens = 200, Temperature = 0.8 });
#pragma warning disable SKEXP0052
            var arguments = new KernelArguments();
            arguments[TextMemoryPlugin.CollectionParam] = MemoryCollectionName;
            arguments[TextMemoryPlugin.LimitParam] = "2";
            arguments[TextMemoryPlugin.RelevanceParam] = "0.8";
            arguments["query"] = "List customers in Boston";

            // Process the user message and get an answer
            var answer = await chatFunction.InvokeAsync(_kernel, arguments);
            Console.WriteLine(answer);
        }

        private void ListPlugins()
        {
            foreach (KernelFunctionMetadata func in _kernel.Plugins.GetFunctionsMetadata())
            {
                Console.WriteLine($"Plugin: {func.PluginName}");
                Console.WriteLine($"   {func.Name}: {func.Description}");

                if (func.Parameters.Count > 0)
                {
                    Console.WriteLine("      Params:");
                    foreach (var p in func.Parameters)
                    {
                        Console.WriteLine($"      - {p.Name}: {p.Description}");
                        Console.WriteLine($"        default: '{p.DefaultValue}'");
                    }
                }

                Console.WriteLine();
            }
        }
    }
}
