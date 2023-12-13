// See https://aka.ms/new-console-template for more information
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.SemanticKernel;
using SemanticKernel1;

var appBuilder = Host.CreateApplicationBuilder(args);
appBuilder.Configuration.Sources.Clear();

IHostEnvironment env = appBuilder.Environment;
appBuilder.Configuration
    .AddJsonFile("appSettings.json", optional: true, reloadOnChange: true)
    .AddJsonFile($"appSettings.{env.EnvironmentName}.json", true, true)
    .AddUserSecrets<Program>();
var settings = new Settings();
appBuilder.Configuration.Bind("AI", settings);

//Create Kernel builder
var builder = new KernelBuilder();
builder.WithAzureOpenAIChatCompletionService(settings.Model, settings.Endpoint, settings.Secret);
IKernel kernel = builder.Build();

var pluginsDirectory = Path.Combine(System.IO.Directory.GetCurrentDirectory(), "..", "..", "..", "..", "plugins");
var funPluginFunctions = kernel.ImportSemanticFunctionsFromDirectory(pluginsDirectory, "ChildrensBookPlugin");
var result = await kernel.RunAsync("Boats", funPluginFunctions["CreateBook"]);
var resultString = result.GetValue<string>();
Console.WriteLine(resultString);