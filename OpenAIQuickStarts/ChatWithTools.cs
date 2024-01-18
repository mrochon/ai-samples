using Azure;
using Azure.AI.OpenAI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ConsoleApp
{
    internal class ChatWithTools
    {
        static ChatCompletionsFunctionToolDefinition getWeatherTool = new ChatCompletionsFunctionToolDefinition()
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
                new JsonSerializerOptions() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }
            ),
        };
        static ChatCompletionsFunctionToolDefinition getHotels = new ChatCompletionsFunctionToolDefinition()
        {
            Name = "get_hotels",
            Description = "Get hotels given location, maximum price or other characteristics",
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
                        MaxPrice = new
                        {
                            Type = "string",
                            Description = "Maximum price",
                        }
                    },
                    Required = new[] { "location" },
                },
                new JsonSerializerOptions() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }
            ),
        };
        public static async Task Run(OpenAISettings openAI)
        {
            var client = new OpenAIClient(new Uri(openAI.Endpoint), new AzureKeyCredential(openAI.Key));
            var options = new ChatCompletionsOptions()
            {
                DeploymentName = openAI.Deployment,
                Messages = {  },
                Tools = { getWeatherTool, getHotels },
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
                Response<ChatCompletions> response = await client.GetChatCompletionsAsync(options);
                foreach (var choice in response.Value.Choices)
                {
                    Console.WriteLine($"[{choice.Message.Role}]: {choice.Message.Content}");
                    if (choice.FinishReason == CompletionsFinishReason.ToolCalls)
                    {
                        ChatRequestAssistantMessage toolCallHistoryMessage = new(choice.Message);
                        options.Messages.Add(toolCallHistoryMessage);
                        foreach (ChatCompletionsToolCall toolCall in choice.Message.ToolCalls)
                        {
                            options.Messages.Add(GetToolCallResponseMessage(toolCall));
                            msgAdded = true;
                        }
                    }
                }
            } while (msgAdded);
        }

        // Purely for convenience and clarity, this standalone local method handles tool call responses.
        static ChatRequestToolMessage GetToolCallResponseMessage(ChatCompletionsToolCall toolCall)
        {
            var functionToolCall = toolCall as ChatCompletionsFunctionToolCall;
            string unvalidatedArguments = functionToolCall!.Arguments;
            if (functionToolCall?.Name == getWeatherTool.Name)
            {
                var functionResultData = (object)null;
                functionResultData = "31 celsius";
                return new ChatRequestToolMessage(functionResultData.ToString(), toolCall.Id);
            } else if (functionToolCall?.Name == getHotels.Name)
            {
                return new ChatRequestToolMessage(GetHotels(unvalidatedArguments), toolCall.Id);
            }
            else
            {
                throw new NotImplementedException();
            }
        }
        static string GetHotels(string parameters)
        {
            return $"Hotels in Seattle: Marriott, Hyatt, Motel 6";
        }
    }
}
