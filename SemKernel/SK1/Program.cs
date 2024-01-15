// See https://aka.ms/new-console-template for more information
// https://github.com/kinfey/SemanticKernelCookBook
// https://github.com/microsoft/semantic-kernel
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Memory;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.Planning;
using Microsoft.SemanticKernel.Plugins.Core;
using SK1;
using System.Buffers.Text;

var appBuilder = Host.CreateApplicationBuilder(args);
appBuilder.Configuration.Sources.Clear();

IHostEnvironment env = appBuilder.Environment;
appBuilder.Configuration
    .AddJsonFile("appSettings.json", optional: true, reloadOnChange: true)
    .AddJsonFile($"appSettings.{env.EnvironmentName}.json", true, true)
    .AddUserSecrets<Program>();
var settings = new Settings();
appBuilder.Configuration.Bind("AI", settings);


var kernel = Kernel.CreateBuilder()
            .AddAzureOpenAIChatCompletion(
                 settings.Model!,   // deployment name
                 settings.Endpoint!, // Azure OpenAI Endpoint
                 settings.Secret!)      // Azure OpenAI Key
            .Build();

var example = Constants.Example.CorePlugin;

switch (example)
{
    case Constants.Example.Summarize:
        string text1 = @"
        1st Law of Thermodynamics - Energy cannot be created or destroyed.
        2nd Law of Thermodynamics - For a spontaneous process, the entropy of the universe increases.
        3rd Law of Thermodynamics - A perfect crystal at zero Kelvin has zero entropy.";

        string text2 = @"
        1. An object at rest remains at rest, and an object in motion remains in motion at constant speed and in a straight line unless acted on by an unbalanced force.
        2. The acceleration of an object depends on the mass of the object and the amount of force applied.
        3. Whenever one object exerts a force on another object, the second object exerts an equal and opposite on the first.";

        var plugin = kernel.LoadPlugins("SummarizePlugin");
        var summary = await kernel.InvokeAsync(plugin["SummarizePlugin"]["Summarize"], new() { ["input"] = text1 });
        Console.WriteLine(summary);
        break;
    case Constants.Example.Joke:
        var plugins = kernel.LoadPlugins("Fun");
        var result = await kernel.InvokeAsync(plugins["Fun"]["Joke"], new() { ["input"] = "What is the meaning of life?" });
        Console.WriteLine(result);
        break;
    case Constants.Example.Plan:
        //var ask = "Tomorrow is Valentine's day. I need to come up with a few date ideas. My significant other likes poems so write them in the form of a poem.";
        var ask = "Check the weather in Guangzhou, use spanish to write emails abount dressing tips based on the results";
        kernel.LoadPlugins("WriterPlugin", "EmailPlugin", "TranslatePlugin");
        var companySearchPluginObj = new CompanySearchPlugin();
        var companySearchPlugin = kernel.ImportPluginFromObject(companySearchPluginObj, "CompanySearchPlugin");
        var plan = kernel.ShowPlanAsync(ask).Result;
#pragma warning disable SKEXP0060
        var planResult = plan.InvokeAsync(kernel, new KernelArguments()).Result;
        Console.WriteLine(planResult);
        break;
    case Constants.Example.Chat:
        Console.WriteLine($"Executed {kernel.ChatAsync().Result} interactions");
        break;
    case Constants.Example.Memory: // Memory; needs a model different from chat-gtp-35
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
        // Memory functionality is experimental
#pragma warning disable SKEXP0011, SKEXP0052, SKEXP0003
        var memoryBuilder = new MemoryBuilder();
        memoryBuilder.WithAzureOpenAITextEmbeddingGeneration(
                settings.Model!,
                "model-id",
                settings.Endpoint!,
                settings.Secret!);

        memoryBuilder.WithMemoryStore(new VolatileMemoryStore());
        var memory = memoryBuilder.Build();
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
        var prompt = "How to add SAML IdP to my IEF journey?";
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
    case Constants.Example.IEF:
        ask = "What IEF policies should I use to authenticate users using either an email address or any work or school authentication.";
        kernel.LoadPlugins("IEF");
        //Console.WriteLine(kernel.InvokeAsync());
        break;
    case Constants.Example.CorePlugin:
        // https://learn.microsoft.com/en-us/semantic-kernel/agents/plugins/out-of-the-box-plugins?tabs=Csharp
        kernel.ImportPluginFromType<TimePlugin>();
        const string promptTemplate = @"
Today is: {{Date}}
Current time is: {{Time}}

Answer to the following questions using JSON syntax, including the data used.
Is it morning, afternoon, evening, or night (morning/afternoon/evening/night)?
Is it weekend time (weekend/not weekend)?";

        var results = await kernel.InvokePromptAsync(promptTemplate);
        Console.WriteLine(results);
        break;

//        {
//            "time": {
//                "hour": 10,
//                  "minute": 51,
//                  "second": 15
//              },
//          "dayOfWeek": "Thursday"
//        }

//        Based on the current time and day of the week, the answers to the questions are as follows:

//          {
//              "morningOrAfternoonOrEveningOrNight": "morning",
//              "weekendTime": "not weekend"
//          }


    default:
        Console.WriteLine("No valid example selected.");
        break;
}

