// See https://aka.ms/new-console-template for more information
// https://github.com/microsoft/semantic-kernel
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.AI.OpenAI;
using SK1;
using System;
using System.Numerics;
using static System.Net.Mime.MediaTypeNames;

var appBuilder = Host.CreateApplicationBuilder(args);
appBuilder.Configuration.Sources.Clear();

IHostEnvironment env = appBuilder.Environment;
appBuilder.Configuration
    .AddJsonFile("appSettings.json", optional: true, reloadOnChange: true)
    .AddJsonFile($"appSettings.{env.EnvironmentName}.json", true, true)
    .AddUserSecrets<Program>();
var settings = new Settings();
appBuilder.Configuration.Bind("AI", settings);


var builder = new KernelBuilder();
builder.AddAzureOpenAIChatCompletion(
         settings.Model!,   // deployment name
         "My model",        // model id
         settings.Endpoint!, // Azure OpenAI Endpoint
         settings.Secret!);      // Azure OpenAI Key

// Alternative using OpenAI
//builder.AddOpenAIChatCompletion(
//         "gpt-3.5-turbo",                  // OpenAI Model name
//         "...your OpenAI API Key...");     // OpenAI API Key

var kernel = builder.Build();

var example = 4;

switch (example)
{
    case 0:
        string text1 = @"
        1st Law of Thermodynamics - Energy cannot be created or destroyed.
        2nd Law of Thermodynamics - For a spontaneous process, the entropy of the universe increases.
        3rd Law of Thermodynamics - A perfect crystal at zero Kelvin has zero entropy.";

        string text2 = @"
        1. An object at rest remains at rest, and an object in motion remains in motion at constant speed and in a straight line unless acted on by an unbalanced force.
        2. The acceleration of an object depends on the mass of the object and the amount of force applied.
        3. Whenever one object exerts a force on another object, the second object exerts an equal and opposite on the first.";

        kernel.Summarize(text1, text2);
        break;
    case 1:
        var runningPrompts = new RunningPrompts(kernel, "Fun");
        Console.WriteLine(await runningPrompts.Run("What is the meaning of life?", "Joke"));
        break;
    case 2:
        var ask = "Tomorrow is Valentine's day. I need to come up with a few date ideas. My significant other likes poems so write them in the form of a poem.";
        kernel.LoadPlugins("SummarizePlugin", "WriterPlugin");
        var plan = kernel.ShowPlanAsync(ask).Result;
        break;
    case 3:
        const string skPrompt = @"
ChatBot can have a conversation with you about any topic.
It can give explicit instructions or say 'I don't know' if it does not have an answer.

{{$history}}
User: {{$userInput}}
ChatBot:";
        var chat = new Chat(
            kernel,
            skPrompt,
            new KernelArguments
            {
                ["history"] = ""
            },
            new OpenAIPromptExecutionSettings
            {
                MaxTokens = 2000,
                Temperature = 0.7,
                TopP = 0.5
            });
        Console.WriteLine($"Done {chat.RunAsync().Result} answers");
        break;
    case 4: // Memory; needs a model different from chat-gtp-35
        const string memoryCollectionName = "SKGitHub";
        var githubFiles = new Dictionary<string, string>()
        {
            ["https://github.com/mrochon/IEFPolicies/blob/main/README.md"]
                = "README: Using IefPolicies to manage Identity Experience Policy set",
            ["https://learn.microsoft.com/en-us/azure/active-directory-b2c/saml-service-provider?tabs=windows&pivots=b2c-custom-policy"]
                = "Register a SAML application in Azure AD B2C",
            ["https://learn.microsoft.com/en-us/azure/active-directory-b2c/identity-provider-generic-saml-options?pivots=b2c-custom-policy"]
                = "Configure SAML identity provider options with Azure Active Directory B2C",
        };
        var memory = kernel.WithMemory(settings);
        Console.WriteLine("Adding some web references to a volatile Semantic Memory.");
        var i = 0;
        foreach (var entry in githubFiles)
        {
            await memory.SaveReferenceAsync(
                collection: memoryCollectionName,
                description: entry.Value,
                text: entry.Value,
                externalId: entry.Key,
                externalSourceName: "GitHub"
            );
            Console.WriteLine($"  URL {++i} saved");
        }
        string prompt = "How to add SAML IdP to my IEF journey?";
        Console.WriteLine("===========================\n" +
                            "Query: " + prompt + "\n");
        var memories = memory.SearchAsync(memoryCollectionName, prompt, limit: 5, minRelevanceScore: 0.77);
        i = 0;
        await foreach (var m in memories)
        {
            Console.WriteLine($"Result {++i}:");
            Console.WriteLine("  URL:     : " + m.Metadata.Id);
            Console.WriteLine("  Title    : " + m.Metadata.Description);
            Console.WriteLine("  Relevance: " + m.Relevance);
            Console.WriteLine();
        }
        break;
    default:
        Console.WriteLine("No valid example selected.");
        break;
}

