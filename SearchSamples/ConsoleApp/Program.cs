// See https://aka.ms/new-console-template for more information
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SearchApps;
using SearchApps.Apps;
using SearchApps.Models;

// Sample data https://github.com/Azure-Samples/azure-search-sample-data/tree/main

Console.WriteLine("Console App starting");

var builder = Host.CreateApplicationBuilder(args);
builder.Configuration.Sources.Clear();
IHostEnvironment env = builder.Environment;
builder.Configuration
    .AddJsonFile("appSettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appSettings.{env.EnvironmentName}.json", true, true)
    .AddUserSecrets<Program>();

builder.Services.AddOptions()
    .Configure<OpenAIOptions>(builder.Configuration.GetSection("OpenAI"))
    .Configure<AzureAISearchOptions>(builder.Configuration.GetSection("AISearch"))
    .Configure<BlobOptions>(builder.Configuration.GetSection("BlobStorage"))
    .AddLogging(builder => builder.AddConsole())
    //.AddHostedService<HelloWorld>();
    //.AddHostedService<Service>();
    //.AddHostedService<VectorSearch>();
    //.AddHostedService<SemanticHybridSearch>();
    .AddHostedService<SkillsPipeline>();

builder.Build().Run();
