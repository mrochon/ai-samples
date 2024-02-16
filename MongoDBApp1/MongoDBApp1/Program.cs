using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SharedLib.Options;
using SharedLib.Services;

var builder = new HostApplicationBuilder();
builder.RegisterConfiguration();
builder.Services.RegisterServices();

//IConfiguration configuration = new ConfigurationBuilder()
//  .AddJsonFile("appSettings.json", optional: true, reloadOnChange: true)
//  .AddUserSecrets<Program>()
//  .AddEnvironmentVariables()
//  .AddCommandLine(args)
//  .Build();

// See https://aka.ms/new-console-template for more information
var host = builder.Build();
var db = host.Services.GetService<MongoDbService>();

var book = db.UpsertBookAsync(new SharedLib.Models.Book("0001", "Paperback", "Sailing", "J Smith", 5)).Result;

Console.WriteLine("Hello, World!");

static class ProgramExtensions
{
    public static void RegisterConfiguration(this HostApplicationBuilder builder)
    {
        //builder.Configuration.AddUserSecrets<Program>(optional: true, reloadOnChange: true);
        builder.Configuration
            .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: true)
            .AddUserSecrets<Program>();
        builder.Services.AddOptions<OpenAi>()
            .Bind(builder.Configuration.GetSection(nameof(OpenAi)));
        builder.Services.AddOptions<MongoDb>()
            .Bind(builder.Configuration.GetSection(nameof(MongoDb)));
    }

    public static void RegisterServices(this IServiceCollection services)
    {
        services.AddSingleton<OpenAiService, OpenAiService>((provider) =>
        {
            var openAiOptions = provider.GetRequiredService<IOptions<OpenAi>>();
            if (openAiOptions is null)
            {
                throw new ArgumentException($"{nameof(IOptions<OpenAi>)} was not resolved through dependency injection.");
            }
            else
            {
                return new OpenAiService(
                    endpoint: openAiOptions.Value?.Endpoint ?? String.Empty,
                    key: openAiOptions.Value?.Key ?? String.Empty,
                    embeddingsDeployment: openAiOptions.Value?.EmbeddingsDeployment ?? String.Empty,
                    completionsDeployment: openAiOptions.Value?.CompletionsDeployment ?? String.Empty,
                    maxConversationTokens: openAiOptions.Value?.MaxConversationTokens ?? String.Empty,
                    maxCompletionTokens: openAiOptions.Value?.MaxCompletionTokens ?? String.Empty,
                    maxEmbeddingTokens: openAiOptions.Value?.MaxEmbeddingTokens ?? String.Empty,
                    logger: provider.GetRequiredService<ILogger<OpenAiService>>()
                );
            }
        });
        services.AddSingleton<MongoDbService, MongoDbService>((provider) =>
        {
            var mongoDbOptions = provider.GetRequiredService<IOptions<MongoDb>>();
            if (mongoDbOptions is null)
            {
                throw new ArgumentException($"{nameof(IOptions<MongoDb>)} was not resolved through dependency injection.");
            }
            else
            {
                return new MongoDbService(
                    connection: mongoDbOptions.Value?.Connection ?? String.Empty,
                    databaseName: mongoDbOptions.Value?.DatabaseName ?? String.Empty,
                    collectionNames: mongoDbOptions.Value?.CollectionNames ?? String.Empty,
                    maxVectorSearchResults: mongoDbOptions.Value?.MaxVectorSearchResults ?? String.Empty,
                    vectorIndexType: mongoDbOptions.Value?.VectorIndexType ?? String.Empty,
                    openAiService: provider.GetRequiredService<OpenAiService>(),
                    logger: provider.GetRequiredService<ILogger<MongoDbService>>()
                );
            }
        });

    }
}

