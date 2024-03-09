using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Planning.Handlebars;

namespace SQLCopilot.Services
{
    internal class CopilotService: IHostedService
    {
        private readonly ILogger<CopilotService> _logger;
        private readonly Kernel _kernel;
        public CopilotService(
            ILogger<CopilotService> logger,
            Kernel kernel)
        {
            _logger = logger;
            _kernel = kernel;
        }
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            Console.WriteLine("CopilotService Started");

            var pluginPath = Path.Combine(System.IO.Directory.GetCurrentDirectory(), "..", "..", "..", "Functions");
            _kernel.CreatePluginFromPromptDirectory(pluginPath);

            var ask = "Which orders are over $100?";
#pragma warning disable SKEXP0060
            var planner = new HandlebarsPlanner() { };
            var plan = await planner.CreatePlanAsync(_kernel, ask);
            Console.WriteLine("Original plan:\n");
            Console.WriteLine(plan);
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            Console.WriteLine("CopilotService Stopped");
            return Task.CompletedTask;
        }
    }
}
