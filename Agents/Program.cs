// See https://aka.ms/new-console-template for more information
using Microsoft.Extensions.Configuration;
using AgentSamples.Models;
using Microsoft.Extensions.Hosting;
using AgentSamples;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

Console.WriteLine("Hello, World!");

var builder = Host.CreateApplicationBuilder(args);
builder.Configuration.Sources.Clear();
IHostEnvironment env = builder.Environment;
builder.Configuration
    .AddJsonFile("appSettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appSettings.{env.EnvironmentName}.json", true, true)
    .AddUserSecrets<Program>();

builder.Services.AddOptions()
    .Configure<AzureOpenAIOptions>(builder.Configuration.GetSection("AzureOpenAI"))
    .AddLogging(builder => builder.AddConsole())
    .AddScoped<IdeaReview>()
    .AddHostedService<Main>();

builder.Build().Run();