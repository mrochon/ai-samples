using System;
using System.ClientModel;
using System.Collections.Generic;
using System.Diagnostics;
using AgentSamples.Models;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents.OpenAI;
using Microsoft.SemanticKernel.ChatCompletion;
using OpenAI.Files;

namespace AgentSamples;

internal class ChartAssistant
{
    protected readonly AzureOpenAIOptions _aiOptions;
    public ChartAssistant(IOptions<AzureOpenAIOptions> options)
    {
        Debug.Assert(options.Value is not null);
        _aiOptions = options.Value;
        Debug.Assert(_aiOptions is not null);
        Debug.Assert(_aiOptions.ChatDeploymentName is not null);
        Debug.Assert(_aiOptions.Endpoint is not null);
        Debug.Assert(_aiOptions.ApiKey is not null);
    }

    public async Task GenerateChartAsync()
    {
#pragma warning disable SKEXP0110
#pragma warning disable SKEXP0001 
        var clientProvider =
            OpenAIClientProvider.ForAzureOpenAI(new ApiKeyCredential(_aiOptions.ApiKey!), new Uri(_aiOptions.Endpoint!));
        Console.WriteLine("Uploading files...");
        var fileClient = clientProvider.Client.GetFileClient();
        var fileDataCountryDetail = await fileClient.UploadFileAsync("./data/PopulationByAdmin1.csv", FileUploadPurpose.Assistants);
        var fileDataCountryList = await fileClient.UploadFileAsync("./data/PopulationByCountry.csv", FileUploadPurpose.Assistants);

        Console.WriteLine("Defining agent...");
        OpenAIAssistantAgent agent =
            await OpenAIAssistantAgent.CreateAsync(
                clientProvider,
                new OpenAIAssistantDefinition(_aiOptions.ChatDeploymentName!)
                {
                    Name = "SampleAssistantAgent",
                    Instructions =
                        """
                Analyze the available data to provide an answer to the user's question.
                Always format response using markdown.
                Always include a numerical index that starts at 1 for any lists or tables.
                Always sort lists in ascending order.
                """,
                    EnableCodeInterpreter = true,
                    CodeInterpreterFileIds = [fileDataCountryList.Value.Id, fileDataCountryDetail.Value.Id],
                },
                new Kernel());

        Console.WriteLine("Creating thread...");
        string threadId = await agent.CreateThreadAsync();

        Console.WriteLine("Ready!");

        try
        {
            bool isComplete = false;
            List<string> fileIds = [];
            do
            {
                Console.WriteLine();
                Console.Write("> ");
                string? input = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(input))
                {
                    continue;
                }
                if (input.Trim().Equals("EXIT", StringComparison.OrdinalIgnoreCase))
                {
                    isComplete = true;
                    break;
                }

                await agent.AddChatMessageAsync(threadId, new ChatMessageContent(AuthorRole.User, input));

                Console.WriteLine();

                bool isCode = false;
                await foreach (StreamingChatMessageContent response in agent.InvokeStreamingAsync(threadId))
                {
                    if (isCode != (response.Metadata?.ContainsKey(OpenAIAssistantAgent.CodeInterpreterMetadataKey) ?? false))
                    {
                        Console.WriteLine();
                        isCode = !isCode;
                    }

                    // Display response.
                    Console.Write($"{response.Content}");

                    // Capture file IDs for downloading.
                    fileIds.AddRange(response.Items.OfType<StreamingFileReferenceContent>().Select(item => item.FileId));
                }
                Console.WriteLine();

                // Download any files referenced in the response.
                await DownloadResponseImageAsync(fileClient, fileIds);
                fileIds.Clear();

            } while (!isComplete);
        }
        finally
        {
            Console.WriteLine();
            Console.WriteLine("Cleaning-up...");
            await Task.WhenAll(
                [
                    agent.DeleteThreadAsync(threadId),
                    agent.DeleteAsync(),
                    fileClient.DeleteFileAsync(fileDataCountryList.Value.Id),
                    fileClient.DeleteFileAsync(fileDataCountryDetail.Value.Id),
                ]);
        }
    }

    private static async Task DownloadResponseImageAsync(FileClient client, ICollection<string> fileIds)
    {
        if (fileIds.Count > 0)
        {
            Console.WriteLine();
            foreach (string fileId in fileIds)
            {
                await DownloadFileContentAsync(client, fileId, launchViewer: true);
            }
        }
    }

    private static async Task DownloadFileContentAsync(FileClient client, string fileId, bool launchViewer = false)
    {
        OpenAIFileInfo fileInfo = client.GetFile(fileId);
        if (fileInfo.Purpose == OpenAIFilePurpose.AssistantsOutput)
        {
            string filePath =
                Path.Combine(
                    Path.GetTempPath(),
                    Path.GetFileName(Path.ChangeExtension(fileInfo.Filename, ".png")));

            BinaryData content = await client.DownloadFileAsync(fileId);
            await using FileStream fileStream = new(filePath, FileMode.CreateNew);
            await content.ToStream().CopyToAsync(fileStream);
            Console.WriteLine($"File saved to: {filePath}.");

            if (launchViewer)
            {
                Process.Start(
                    new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = $"/C start {filePath}"
                    });
            }
        }
    }
}