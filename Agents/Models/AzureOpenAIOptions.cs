using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AgentSamples.Models
{
    internal class AzureOpenAIOptions
    {
        public string? ChatDeploymentName { get; set; }
        public string? Endpoint { get; set; }
        public string? ApiKey { get; set; }
    }
}
