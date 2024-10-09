using AgentSamples.Models;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Agents.Chat;
using Microsoft.SemanticKernel.ChatCompletion;

namespace AgentSamples
{
    internal class RoundRobinIdeaReview: ChattingAgentsBase
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

        public RoundRobinIdeaReview(IOptions<AzureOpenAIOptions> options): base(options)
        { }

        public override async Task GroupChatAsync()
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

    }
}
