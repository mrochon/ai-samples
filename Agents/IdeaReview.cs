using AgentSamples.Models;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Agents.Chat;
using Microsoft.SemanticKernel.Agents.OpenAI;
using Microsoft.SemanticKernel.ChatCompletion;

namespace AgentSamples
{
    internal class IdeaReview
    {
        private const string ReviewerName = "ArtDirector";
        private const string ReviewerInstructions =
            """
        You are an art director who has opinions about copywriting born of a love for David Ogilvy.
        The goal is to determine if the given copy is acceptable to print.
        If so, state that it is approved.
        If not, provide insight on how to refine suggested copy without example.
        """;

        private const string CopyWriterName = "CopyWriter";
        private const string CopyWriterInstructions =
            """
        You are a copywriter with ten years of experience and are known for brevity and a dry humor.
        The goal is to refine and decide on the single best copy as an expert in the field.
        Only provide a single proposal per response.
        You're laser focused on the goal at hand.
        Don't waste time with chit chat.
        Consider suggestions when refining an idea.
        """;

        private readonly AzureOpenAIOptions _aiOptions;
        public IdeaReview(IOptions<AzureOpenAIOptions> options)
        {
            if((options is null) || (options.Value is null))
                throw new ArgumentException("options is null");
            _aiOptions = options.Value;
        }

        public async Task UseAgentGroupChatWithTwoAgentsAsync()
        {
            // Define the agents
#pragma warning disable SKEXP0110 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
            ChatCompletionAgent agentReviewer =
                new()
                {
                    Instructions = ReviewerInstructions,
                    Name = ReviewerName,
                    Kernel = this.CreateKernelWithChatCompletion(),
                };
            ChatCompletionAgent agentWriter =
                new()
                {
                    Instructions = CopyWriterInstructions,
                    Name = CopyWriterName,
                    Kernel = this.CreateKernelWithChatCompletion(),
                };

            // Create a chat for agent interaction.
            AgentGroupChat chat =
                new(agentWriter, agentReviewer)
                {
                    ExecutionSettings =
                        new()
                        {
                            // Here a TerminationStrategy subclass is used that will terminate when
                            // an assistant message contains the term "approve".
                            TerminationStrategy =
                                new ApprovalTerminationStrategy()
                                {
                                    // Only the art-director may approve.
                                    Agents = [agentReviewer],
                                    // Limit total number of turns
                                    MaximumIterations = 10,
                                }
                        }
                };

            // Invoke chat and display messages.
            ChatMessageContent input = new(AuthorRole.User, "concept: maps made out of egg cartons.");
            chat.AddChatMessage(input);
            this.WriteAgentChatMessage(input);

            await foreach (ChatMessageContent response in chat.InvokeAsync())
            {
                this.WriteAgentChatMessage(response);
            }

            Console.WriteLine($"\n[IS COMPLETED: {chat.IsComplete}]");
        }

        private sealed class ApprovalTerminationStrategy : TerminationStrategy
        {
            // Terminate when the final message contains the term "approve"
            protected override Task<bool> ShouldAgentTerminateAsync(Agent agent, IReadOnlyList<ChatMessageContent> history, CancellationToken cancellationToken)
                => Task.FromResult(history[history.Count - 1].Content?.Contains("approve", StringComparison.OrdinalIgnoreCase) ?? false);
        }

        protected Kernel CreateKernelWithChatCompletion()
        {
            var builder = Kernel.CreateBuilder();
            builder.AddAzureOpenAIChatCompletion(
                _aiOptions.ChatDeploymentName,
                _aiOptions.Endpoint,
                _aiOptions.ApiKey);
            return builder.Build();
        }

        protected void WriteAgentChatMessage(ChatMessageContent message)
        {
            // Include ChatMessageContent.AuthorName in output, if present.
#pragma warning disable SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
            string authorExpression = message.Role == AuthorRole.User ? string.Empty : $" - {message.AuthorName ?? "*"}";
#pragma warning restore SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
                                 // Include TextContent (via ChatMessageContent.Content), if present.
            string contentExpression = string.IsNullOrWhiteSpace(message.Content) ? string.Empty : message.Content;
            bool isCode = message.Metadata?.ContainsKey(OpenAIAssistantAgent.CodeInterpreterMetadataKey) ?? false;
            string codeMarker = isCode ? "\n  [CODE]\n" : " ";
            Console.WriteLine($"\n# {message.Role}{authorExpression}:{codeMarker}{contentExpression}");

            // Provide visibility for inner content (that isn't TextContent).
            foreach (KernelContent item in message.Items)
            {
                if (item is AnnotationContent annotation)
                {
                    Console.WriteLine($"  [{item.GetType().Name}] {annotation.Quote}: File #{annotation.FileId}");
                }
                else if (item is FileReferenceContent fileReference)
                {
                    Console.WriteLine($"  [{item.GetType().Name}] File #{fileReference.FileId}");
                }
#pragma warning disable SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
                else if (item is ImageContent image)
                {
                    Console.WriteLine($"  [{item.GetType().Name}] {image.Uri?.ToString() ?? image.DataUri ?? $"{image.Data?.Length} bytes"}");
                }
                else if (item is FunctionCallContent functionCall)
                {
                    Console.WriteLine($"  [{item.GetType().Name}] {functionCall.Id}");
                }
                else if (item is FunctionResultContent functionResult)
                {
                    Console.WriteLine($"  [{item.GetType().Name}] {functionResult.CallId}");
                }
#pragma warning restore SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
            }
        }

    }
}
