using Azure;
using Azure.AI.OpenAI;
using Microsoft.Extensions.Configuration;
using ConsoleApp;
using System.Text;
using System.Text.Json;

// Create a configuration object from the json file
IConfigurationRoot config = new ConfigurationBuilder()
    .AddJsonFile("appSettings.json", optional: true)
    .AddUserSecrets<Program>()
    .Build();

var openAI = config.GetSection("OpenAI").Get<OpenAISettings>();
if (string.IsNullOrEmpty(openAI?.Endpoint) || string.IsNullOrEmpty(openAI?.Key) || string.IsNullOrEmpty(openAI?.Deployment))
    throw new Exception("One or more OpenAI settings are not configured");

var type = "chatWithTools";
switch(type)
{
    case "ask":
        await Ask(openAI!.Endpoint, openAI.Key, openAI.Deployment);
        break;
    case "chat1":
        await Chat1(openAI!.Endpoint, openAI.Key, openAI.Deployment);
        break;
    case "nonchat":
        NonChat(openAI!.Endpoint, openAI.Key, openAI.Deployment);
        break;
    case "conversation":
        await Conversation(openAI!.Endpoint, openAI.Key, openAI.Deployment);
        break;
    case "conversationwithfunctions":
        await ConversationWithFunctions(openAI!.Endpoint, openAI.Key, openAI.Deployment);
        break;
    case "chatWithTools":
        await ChatWithTools.Run(openAI!);
        break;
    default:
        Console.WriteLine("Bad choice!!!");
        break;
}
//Console.ReadLine();

async Task Ask(string endpoint, string key, string deploymentOrModelName)
{
    // Enter the deployment name you chose when you deployed the model.
    //string engine = "text-davinci-003";
    var engine = openAI.Deployment;

    OpenAIClient client = new(new Uri(openAI.Endpoint), new AzureKeyCredential(openAI.Key));

    string prompt = "When was Microsoft founded?";
    Console.Write($"Input: {prompt}\n");

    Response<Completions> completionsResponse =
        await client.GetCompletionsAsync(new CompletionsOptions(deploymentOrModelName, new string[] {prompt }));
    string completion = completionsResponse.Value.Choices[0].Text;
    Console.WriteLine($"Chatbot: {completion}");
}


async Task Chat1(string endpoint, string key, string deploymentOrModelName)
{
    OpenAIClient client = new(new Uri(openAI.Endpoint), new AzureKeyCredential(openAI.Key));

    var chatCompletionsOptions = new ChatCompletionsOptions(deploymentOrModelName, new ChatRequestMessage[]
        {
            new ChatRequestSystemMessage("You are a helpful assistant."),
            new ChatRequestUserMessage("Does Azure OpenAI support customer managed openAI.Keys?"),
            new ChatRequestSystemMessage("Yes, customer managed openAI.Keys are supported by Azure OpenAI."),
            new ChatRequestUserMessage("Do other Azure AI services support this too?"),
        });

    using (StreamingResponse<StreamingChatCompletionsUpdate> response = await client.GetChatCompletionsStreamingAsync(chatCompletionsOptions))
    {
        var txt = new StringBuilder();
        await foreach (var choice in response)
        {
            if (choice.ContentUpdate != null)
            {
                var txt1 = choice.ContentUpdate;
                txt.Append(txt1);
                // if (choice.Completion.Length > 0 && choice.Completion[^1] != ' ')
                //     txt.Append(' ');
            }
        }

        Console.WriteLine();
    }
}

void NonChat(string endpoint, string key, string deploymentOrModelName)
{
    OpenAIClient client = new(new Uri(openAI.Endpoint), new AzureKeyCredential(openAI.Key));

    var response = client.GetChatCompletions(new ChatCompletionsOptions(deploymentOrModelName, new ChatRequestMessage[]
        {
            new ChatRequestSystemMessage(@"""You are an assistant designed to extract entities from text. Users will paste in a string of text and you will respond with entities you've extracted from the text as a JSON object. Here's an example of your output format:
            {
                ""name"": """",
                ""company"": """",
                ""phone_number"": """",
                ""subject"": """",
            }"""),
            new ChatRequestUserMessage("Hello. My name is Robert Smith. I'm calling from Contoso Insurance, Delaware. My colleague mentioned that you are interested in learning about our comprehensive benefits policy. Please call me back at (555) 346-9322. I would like to go over the benefits."),
        }));

    Console.WriteLine(response.Value.Choices[0].Message.Content);

    Console.WriteLine();
}

async Task Conversation(string endpoint, string key, string deploymentOrModelName)
{
    OpenAIClient client = new(new Uri(openAI.Endpoint), new AzureKeyCredential(openAI.Key));

    var messages = new ChatCompletionsOptions(deploymentOrModelName, new ChatRequestMessage[]
    {
            new ChatRequestSystemMessage("Users will ask you questions. Answer in Danish."),
    });

    do
    {
        Console.Write("User: ");
        string? userMessage = Console.ReadLine();
        if (string.IsNullOrEmpty(userMessage))
            break;
        messages.Messages.Add(new ChatRequestUserMessage(userMessage));
        StreamingResponse<StreamingChatCompletionsUpdate> response = await client.GetChatCompletionsStreamingAsync(messages);

        var streamingChatCompletions = response.GetRawResponse();

        await foreach (var choice in response)
        {
            Console.Write(choice.ContentUpdate);
            Console.WriteLine();
        }
        messages.Messages.Add(new ChatRequestAssistantMessage("Now start answering in English"));
        Console.WriteLine();
    } while (true);

    Console.WriteLine();
}

async Task ConversationWithFunctions(string endpoint, string key, string deploymentOrModelName)
{
    OpenAIClient client = new(new Uri(openAI.Endpoint), new AzureKeyCredential(openAI.Key));


    var getWeatherTool = new ChatCompletionsFunctionToolDefinition()
    {
        Name = "get_current_weather",
        Description = "Get the current weather in a given location",
        Parameters = BinaryData.FromObjectAsJson(
    new
    {
        Type = "object",
        Properties = new
        {
            Location = new
            {
                Type = "string",
                Description = "The city and state, e.g. San Francisco, CA",
            },
            Unit = new
            {
                Type = "string",
                Enum = new[] { "celsius", "fahrenheit" },
            }
        },
        Required = new[] { "location" },
    },
    new JsonSerializerOptions() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }),
    };
    var functions = new FunctionDefinition[]
    {
        new FunctionDefinition()
        {
            Name = "getHotels",
            Description = "Retrieves hotels from the search index based on the parameters provided",
            Parameters = BinaryData.FromObjectAsJson(new
            {
                type = "object",
                required = new[] { "location" },
                properties =  new {
                    location = new { type = "string", description = "The location of the hotel (i.e. Seattle, WA)" } ,
                    max_price = new { type = "number", description = "The maximum price for the hotel" } ,
                    features = new { type = "string", description = "A comma separated list of features (i.e. beachfront, free wifi, etc.)" } 
                }
            })
        }
    };
    var options = new ChatCompletionsOptions(deploymentOrModelName, new ChatRequestMessage[]
    {
        new ChatRequestSystemMessage("Please use functions whenever possible."),
    })
    { 
        ChoiceCount = 1,
        Functions = functions,
        MaxTokens = 100,
        ResponseFormat = ChatCompletionsResponseFormat.Text,
    };
 
    var msgAdded = false;
    do
    {
        if (!msgAdded)
        {
            Console.Write("User: ");
            string? userMessage = Console.ReadLine();
            if (string.IsNullOrEmpty(userMessage))
                break;
            options.Messages.Add(new ChatRequestUserMessage(userMessage));
        }
        msgAdded = false;
        var response = await client.GetChatCompletionsAsync(options);
        foreach(var choice in response.Value.Choices)
        {
            Console.WriteLine($"[{choice.Message.Role}]: {choice.Message.Content}");
            if (choice.FinishReason == CompletionsFinishReason.FunctionCall)
            {
                Console.WriteLine($"{choice.Message.FunctionCall.Name}({choice.Message.FunctionCall.Arguments})");
                switch (choice.Message.FunctionCall.Name)
                {
                    case "getHotels":
                        var p = choice.Message.FunctionCall.Arguments;
                        options.Messages.Add(new ChatRequestToolMessage(GetHotels(p), "x"));
                        msgAdded = true;
                        break;
                    default:
                        Console.WriteLine("Function not found");
                        break;
                }
            }
        }
        Console.WriteLine();
    } while (true);

    Console.WriteLine();
}

string GetHotels(string parameters)
{
    return $"Hotels in Seattle: Marriott, Hyatt, Motel 6";
}
