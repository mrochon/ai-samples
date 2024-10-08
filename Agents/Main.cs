using AgentSamples;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

internal class Main : IHostedService
{
    private readonly ILogger<Main> _logger;
    private readonly IdeaReview _ideaReview;

    public Main(
        ILogger<Main> logger,
        IdeaReview ideaReview
        )
    {
        _logger = logger;
        _ideaReview = ideaReview;
    }
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogTrace("Main Started");
        await _ideaReview.UseAgentGroupChatWithTwoAgentsAsync();
        _logger.LogTrace("done");
        await StopAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        Console.WriteLine("Stopped");
        return Task.CompletedTask;
    }

}