using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Planning.Handlebars;

namespace SK1
{
    internal class PluginsContainer
    {
        Kernel _kernel;
        HandlebarsPlanner _planner;

        public PluginsContainer(Kernel kernel, params string[] pluginNames)
        {
            _kernel = kernel;

            _planner = new HandlebarsPlanner();
            var pluginPath = Path.Combine(System.IO.Directory.GetCurrentDirectory(), "..", "..", "..", "plugins");
            foreach(var plugin in pluginNames)
            {
                _kernel.ImportPluginFromPromptDirectory(Path.Combine(pluginPath, plugin));
            }
        }

        public async Task<HandlebarsPlan> ShowPlanAsync(string prompt)
        {
            var plan = await _planner.CreatePlanAsync(_kernel, prompt);

            Console.WriteLine("Original plan:\n");
            Console.WriteLine(plan);
            return plan;
        }
    }
}
