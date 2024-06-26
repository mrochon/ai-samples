﻿using Azure.Search.Documents.Indexes;
using Azure;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.AzureAISearch;
using SKApps.Plugins;
using SKApps.Services;
using System.Text.Json;
using Microsoft.Extensions.Options;
using SKApps.Models;
using Microsoft.Extensions.DependencyInjection;

#pragma warning disable SKEXP0010

namespace SKApps
{
    public class AISearchApp : IHostedService
    {
        private readonly ILogger<AISearchApp> _logger;
        private readonly IOptions<OpenAIOptions> _options;
        private readonly IOptions<AzureAISearchOptions> _searchOptions;
        private readonly Kernel _kernel;
        public AISearchApp(
            ILogger<AISearchApp> logger,
            IOptions<OpenAIOptions> options,
            IOptions<AzureAISearchOptions> searchOptions)
        {
            _logger = logger;
            _options = options;
            ArgumentNullException.ThrowIfNull(logger);
            ArgumentNullException.ThrowIfNull(options);
            ArgumentNullException.ThrowIfNull(options.Value);
            ArgumentNullException.ThrowIfNull(options.Value.Endpoint);
            ArgumentNullException.ThrowIfNull(options.Value.Key);
            ArgumentNullException.ThrowIfNull(searchOptions);

            var kernelBuilder = Kernel.CreateBuilder();
            kernelBuilder.Services
                 .AddLogging(builder => builder.AddConsole())
                 .AddSingleton(searchOptions)
                 .AddSingleton(options)
                //.AddSingleton<SearchIndexClient>((_) => new SearchIndexClient(new Uri(searchOptions.Value.Endpoint), new AzureKeyCredential(searchOptions.Value.Key)))
                .AddSingleton<ISearchService, AzureAISearchService>()
                .AddSingleton<OpenAIService>();
            kernelBuilder
                //.AddOpenAITextEmbeddingGeneration(options.Value.EmbeddingsDeployment, options.Value.Key)
                .AddAzureOpenAIChatCompletion(options.Value.CompletionsDeployment, options.Value.Endpoint, options.Value.Key)
                .Plugins.AddFromType<AzureAISearchPlugin>();

            // Create kernel
            _kernel = kernelBuilder.Build();
        }
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogTrace($"{this.GetType().Name} Started");
            // Query with index name and search fields.
            var arguments = new KernelArguments {
                ["searchFields"] = JsonSerializer.Serialize(new List<string> { "DescriptionVector" }),
                ["filter"] = "Category eq 'Budget'"
            };
            var result = await _kernel.InvokePromptAsync(
                "{{AISearch 'Seaview apartments, small' collection='hotels' searchFields=$searchFields filter=$filter}} I am looking for a hotel with seaview?",
                arguments);

            Console.WriteLine(result);

            _logger.LogTrace($"{this.GetType().Name} Done");
            await StopAsync(cancellationToken);
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogTrace($"{this.GetType().Name} Stopping");
            return Task.CompletedTask;
        }
    }
}
