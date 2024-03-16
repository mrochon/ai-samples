using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Memory;
using Microsoft.SemanticKernel.Connectors.OpenAI;

using Models;
using SQLCopilot.Services;
using Microsoft.Extensions.Options;

Console.WriteLine("Hello, World!");

var builder = Host.CreateApplicationBuilder(args);
builder.Configuration.Sources.Clear();
IHostEnvironment env = builder.Environment;
builder.Configuration
    .AddJsonFile("appSettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appSettings.{env.EnvironmentName}.json", true, true)
    .AddUserSecrets<Program>();

#pragma warning disable SKEXP0004
builder.Services.AddOptions()
    .Configure<OpenAIOptions>(builder.Configuration.GetSection("OpenAI"))
    .AddLogging(builder => builder.AddSimpleConsole())
    .AddHostedService<CopilotService>();

builder.Build().Run();
