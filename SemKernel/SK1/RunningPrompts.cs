using Microsoft.SemanticKernel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SK1
{
    internal class RunningPrompts
    {
        Kernel _kernel;
        IKernelPlugin _kernelFunctions;
        public RunningPrompts(Kernel kernel, string pluginName)
        {
            _kernel = kernel;
            var pluginPath = Path.Combine(System.IO.Directory.GetCurrentDirectory(), "..", "..", "..", "plugins", pluginName);
            _kernelFunctions = _kernel.ImportPluginFromPromptDirectory(pluginPath);
        }

        public async Task<FunctionResult?> Run(string input, string function)
        {
            var arguments = new KernelArguments(input);
            var result = await _kernel.InvokeAsync(_kernelFunctions[function], arguments);
            return result;
        }
    }
}
