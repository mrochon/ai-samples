// See https://aka.ms/new-console-template for more information
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using MongoDB01;
using SharedRAG.Models;
using SharedRAG.Services;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;

var builder = Host.CreateApplicationBuilder(args);
builder.Configuration.Sources.Clear();
IHostEnvironment env = builder.Environment;
builder.Configuration
    .AddJsonFile("appSettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appSettings.{env.EnvironmentName}.json", true, true)
    .AddUserSecrets<Program>();

builder.Services.AddOptions()
    .Configure<MongoDBOptions>(builder.Configuration.GetSection("MongoDB"))
    .Configure<OpenAIOptions>(builder.Configuration.GetSection("OpenAI"))
    .Configure<BlobOptions>(builder.Configuration.GetSection("BlobStorage"))
    .Configure<AzureAISearchOptions>(builder.Configuration.GetSection("AISearch"))
    .AddLogging(builder => builder.AddConsole())
    .AddScoped<OpenAIService>()
    .AddScoped<AtlasMongoDBService>()
    .AddScoped<BlobService>()
    .AddScoped<AzureAISearchService>()
    .AddHostedService<MainAISearch>();

builder.Build().Run();
