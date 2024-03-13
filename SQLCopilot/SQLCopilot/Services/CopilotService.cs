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
using System.Data;
using System.IO.Pipes;
using System.Text;

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
Generate SQL query based on user input or ask for more details in case you need more information to generate the query. The generated query must specify names of columns to return rather than using the ""*"" (asterisk) operator.
If you don't have enough information for SQL query generation - respond with your question starting with ""ChatBot: "" prefix. For example: ""ChatBot: What details do you need about your customer?"".
If you have enough information for SQL query generation - generate a query and return it starting with ""SQL: "" prefix. For example: ""SQL: SELECT FirstName, LastName FROM Contacts"". 
If the user input does not give you enough information about which columns to use in the query, respond with your question starting with ""ChatBot:"". 

Chat: {{$history}}
User input: {{$userInput}}
Schema: {{recall $userInput}}

###
# The following examples are for the SQLCopilot plugin

userInput: Show customers in New York
chatbot: ChatBot: What specific data do you want about these customers?

userInput: List orders worth more than $100
chatbot: ChatBot: what data do you need about those orders?

userInput: List names of customers in Boston
chatbot: SQL: SELECT name FROM customers WHERE city = 'Boston'

userInput : List order ids for customer 123?
chatbot: SQL: SELECT id FROM orders WHERE customerId = 123

userInput: Who ordered product XYZ?
chatbot: ChatBot: what data do you need about that customer?

User: {{$userInput}}
ChatBot: ";

            string[] queries = new string[]
            {
                "Show customers in Boston",
                "List orders worth more than $100",
                "List names of customers in Boston",
                "List orders ids for customer 123",
                "Who ordered product XYZ?",
                "I need order data"
            };
            StringBuilder history = new StringBuilder();

            var chatFunction = _kernel.CreateFunctionFromPrompt(skPrompt, new OpenAIPromptExecutionSettings { MaxTokens = 200, Temperature = 0.8 });
#pragma warning disable SKEXP0052
            var arguments = new KernelArguments();
            arguments[TextMemoryPlugin.CollectionParam] = MemoryCollectionName;
            arguments[TextMemoryPlugin.LimitParam] = "2";
            arguments[TextMemoryPlugin.RelevanceParam] = "0.9";
            foreach(var userInput in queries)
            {
                //Console.Write('>');
                //var userInput = Console.ReadLine();
                //if (String.IsNullOrEmpty(userInput))
                //{
                //    break;
                //}
                Console.WriteLine($">{userInput}");
                arguments["userInput"] = userInput;
                var answer = await chatFunction.InvokeAsync(_kernel, arguments);
                history.Clear();
                while(answer.ToString().StartsWith("ChatBot"))
                {
                    Console.WriteLine(answer);
                    Console.Write(">>");
                    var inp = Console.ReadLine();
                    var result = $"\nUser: {userInput}\nChatBot: {answer}\n";
                    history.Append(result);
                    arguments["history"] = history;
                    arguments["userInput"] = inp;
                    answer = await chatFunction.InvokeAsync(_kernel, arguments);
                }
                Console.WriteLine(answer);
            };

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
