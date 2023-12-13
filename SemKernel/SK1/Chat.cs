using HandlebarsDotNet;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.AI.OpenAI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SK1
{
    internal class Chat
    {
        Kernel _kernel;
        KernelFunction _kernelFunction;
        KernelArguments _kernelArgs;
        public Chat(Kernel kernel, string prompt, KernelArguments initialArgs, OpenAIPromptExecutionSettings settings)
        {
            _kernel = kernel;
            _kernelFunction = kernel.CreateFunctionFromPrompt(prompt, settings);
            _kernelArgs = initialArgs;
        }
        public async Task<int> RunAsync()
        {
            Console.WriteLine("Start asking me questions...");
            var responses = 0;
            do
            {
                var input = Console.ReadLine();
                if (input!.StartsWith("q", StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }
                ++responses;
                _kernelArgs["userInput"] = input;
                var answer = await _kernelFunction.InvokeAsync(_kernel, _kernelArgs);
                var result = $"\nUser: {input}\nMelody: {answer}\n";
                _kernelArgs["history"] += result;
                Console.WriteLine(result);
            } while (true);
            return responses;
        }
    }
}
