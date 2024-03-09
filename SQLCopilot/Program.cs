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

builder.Services.AddOptions()
    .Configure<OpenAIOptions>(builder.Configuration.GetSection("OpenAI"))
    .AddLogging(builder => builder.AddSimpleConsole())
    .AddSingleton<Kernel>(sp =>
    {
        var options = sp.GetRequiredService<IOptions<OpenAIOptions>>().Value;
        return Kernel.CreateBuilder()
            .AddAzureOpenAIChatCompletion(
                 "mr",   // deployment name
                 options.Endpoint!, // Azure OpenAI Endpoint
                 options.Key!)      // Azure OpenAI Key
            .Build();
    })
    .AddHostedService<CopilotService>();

builder.Build().Run();
