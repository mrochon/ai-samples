using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SQLCopilot.Services
{
#pragma warning disable SKEXP0004
    internal sealed class PromptFilter : IPromptFilter
    {
        private readonly ILogger<PromptFilter> _logger;
        public PromptFilter(ILogger<PromptFilter> logger)
        {
            this._logger = logger;
        }

        /// <summary>
        /// Method which is called after a prompt is rendered.
        /// </summary>
        public void OnPromptRendered(PromptRenderedContext context)
        {
            context.RenderedPrompt += " NO SEXISM, RACISM OR OTHER BIAS/BIGOTRY";
            this._logger.LogInformation($"PromptRendered: {context.RenderedPrompt}");
        }

        /// <summary>
        /// Method which is called before a prompt is rendered.
        /// </summary>
        public void OnPromptRendering(PromptRenderingContext context)
        {
            foreach(var arg in context.Arguments)
            {
                this._logger.LogInformation($"Argument: {arg.Key} = {arg.Value}");
            }
            if (context.Arguments.ContainsName("card_number"))
            {
                context.Arguments["card_number"] = "**** **** **** ****";
            }
        }
    }
}
