using Microsoft.SemanticKernel;
using InteractiveKernel = Microsoft.DotNet.Interactive.Kernel;

// ReSharper disable InconsistentNaming

public static class KernelExtensions
{
    public static Dictionary<string, KernelPlugin> LoadPlugins(this Microsoft.SemanticKernel.Kernel kernel, params string[] pluginNames)
    {
        Dictionary<string, KernelPlugin> plugins = new Dictionary<string, KernelPlugin>();
        var pluginPath = Path.Combine(System.IO.Directory.GetCurrentDirectory(), "..", "..", "..", "plugins");
        foreach (var plugin in pluginNames)
        {
            plugins.Add(plugin, kernel.CreatePluginFromPromptDirectory(Path.Combine(pluginPath, plugin)));
        }
        return plugins;
    }
}