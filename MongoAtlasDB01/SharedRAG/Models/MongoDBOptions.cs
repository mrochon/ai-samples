using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharedRAG.Models
{
    public class MongoDBOptions
    {
        public string Connection { get; set; }
        public string DatabaseName { get; set; }
    }
}
