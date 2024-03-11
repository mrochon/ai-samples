using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Models
{
    internal class OpenAIOptions
    {
        public string? ChatDeployment { get; set; }
        public string? EmbeddingDeployment { get; set; }
        public string? Endpoint { get; set; }
        public string? Key { get; set; }
    }
}
