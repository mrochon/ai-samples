using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharedRAG.Models
{
    public class AzureAISearchOptions
    {
        public string? Endpoint { get; set; }
        public string? Key { get; set; }
        public string? AdminKey { get; set; }
        public string? IndexName { get; set; }
    }
}
