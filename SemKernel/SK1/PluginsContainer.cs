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
        /*
{{!-- Step 1: Initialize an array of date ideas --}}
{{set "dateIdeas" (array)}}

{{!-- Step 2: Add date ideas to the array --}}
{{set "dateIdeas" (concat (get "dateIdeas") "A romantic picnic in the park,")}}
{{set "dateIdeas" (concat (get "dateIdeas") "A candlelit dinner for two,")}}
{{set "dateIdeas" (concat (get "dateIdeas") "A moonlit walk on the beach,")}}
{{set "dateIdeas" (concat (get "dateIdeas") "A cozy movie night at home,")}}
{{set "dateIdeas" (concat (get "dateIdeas") "A couples' spa day,")}}

{{!-- Step 3: Create the poem using the date ideas --}}
{{set "poem" (concat "On Valentine's day, let's have some fun," (get "dateIdeas" 0) "\n" (get "dateIdeas" 1) "\n" (get "dateIdeas" 2) "\n" (get "dateIdeas" 3) "\n" (get "dateIdeas" 4))}}

{{!-- Step 4: Print the poem to the screen --}}
{{json (get "poem")}}
        */

    }
}
