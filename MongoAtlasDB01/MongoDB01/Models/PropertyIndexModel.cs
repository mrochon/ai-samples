using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MongoDB01.Models
{
    public class PropertyIndexModel
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public ReadOnlyMemory<float> Embedding { get; set; }
    }
}
