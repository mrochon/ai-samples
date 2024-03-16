using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SQLCopilot.Services
{
#pragma warning disable SKEXP0001
    internal sealed class PromptFilter : IPromptFilter
    {
        public PromptFilter()
        {
        }

        /// <summary>
        /// Method which is called after a prompt is rendered.
        /// </summary>
        public void OnPromptRendered(PromptRenderedContext context)
        {
            Console.WriteLine("PromptRendered");
            Console.WriteLine(context.RenderedPrompt);
            //context.RenderedPrompt += " NO SEXISM, RACISM OR OTHER BIAS/BIGOTRY";
        }

        /// <summary>
        /// Method which is called before a prompt is rendered.
        /// </summary>
        public void OnPromptRendering(PromptRenderingContext context)
        {
            Console.WriteLine("PromptRendering");
            foreach(var arg in context.Arguments)
            {
                Console.WriteLine($"Argument: {arg.Key} = {arg.Value}");
            }
            //if (context.Arguments.ContainsName("card_number"))
            //{
            //    context.Arguments["card_number"] = "**** **** **** ****";
            //}
        }
    }
}
