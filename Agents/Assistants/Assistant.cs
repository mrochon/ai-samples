using Microsoft.SemanticKernel.Agents.OpenAI;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using AgentSamples.Models;
using Azure.AI.OpenAI;
using System.ClientModel;

namespace AgentSamples;

internal class Assistant: ChattingAgentsBase
{
    public Assistant(IOptions<AzureOpenAIOptions> options) : base(options)
    {
    }

    public override Task GroupChatAsync()
    {
        throw new NotSupportedException();
    }

    public async Task GenerateChartAsync()
    {
#pragma warning disable SKEXP0110 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
        OpenAIClientProvider provider = this.GetClientProvider();

        OpenAIFileClient fileClient = provider.Client.GetOpenAIFileClient();

        // Define the agent
        OpenAIAssistantAgent agent =
            await OpenAIAssistantAgent.CreateAsync(
                provider,
                definition: new OpenAIAssistantDefinition(this.Model)
                {
                    Instructions = AgentInstructions,
                    Name = AgentName,
                    EnableCodeInterpreter = true,
                    Metadata = AssistantSampleMetadata,
                },
                kernel: new());

        // Create a chat for agent interaction.
        AgentGroupChat chat = new();

        // Respond to user input
        try
        {
            await InvokeAgentAsync(
                """
            Display this data using a bar-chart:

            Banding  Brown Pink Yellow  Sum
            X00000   339   433     126  898
            X00300    48   421     222  691
            X12345    16   395     352  763
            Others    23   373     156  552
            Sum      426  1622     856 2904
            """);

            await InvokeAgentAsync("Can you regenerate this same chart using the category names as the bar colors?");
            await InvokeAgentAsync("Perfect, can you regenerate this as a line chart?");
        }
        finally
        {
            await agent.DeleteAsync();
        }

        // Local function to invoke agent and display the conversation messages.
        async Task InvokeAgentAsync(string input)
        {
            ChatMessageContent message = new(AuthorRole.User, input);
            chat.AddChatMessage(new(AuthorRole.User, input));
            this.WriteAgentChatMessage(message);

            await foreach (ChatMessageContent response in chat.InvokeAsync(agent))
            {
                this.WriteAgentChatMessage(response);
                await this.DownloadResponseImageAsync(fileClient, response);
            }
        }
    }
    public OpenAIClientProvider ClientProvider(HttpClient? httpClient = null)
    {
        AzureOpenAIClientOptions clientOptions = CreateAzureClientOptions(httpClient);

        return new OpenAIClientProvider( AzureOpenAIClient(new Uri(_aiOptions.Endpoint), _aiOptions.ApiKey!, clientOptions); //, CreateConfigurationKeys(new Uri(_aiOptions.Endpoint), httpClient));
    }
    private static AzureOpenAIClientOptions CreateAzureClientOptions(HttpClient? httpClient)
    {
        AzureOpenAIClientOptions options = new()
        {
            UserAgentApplicationId = HttpHeaderConstant.Values.UserAgent
        };

        ConfigureClientOptions(httpClient, options);

        return options;
    }
    private static IEnumerable<string> CreateConfigurationKeys(Uri? endpoint, HttpClient? httpClient)
    {
        if (endpoint != null)
        {
            yield return endpoint.ToString();
        }

        if (httpClient is not null)
        {
            if (httpClient.BaseAddress is not null)
            {
                yield return httpClient.BaseAddress.AbsoluteUri;
            }

            foreach (string header in httpClient.DefaultRequestHeaders.SelectMany(h => h.Value))
            {
                yield return header;
            }
        }
    }
}
