using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SearchApps.Models
{
    public class BlobOptions
    {
        // Populated at run-time from the other options
        public string? ConnectionString { get; set; }

        public string? AccountName { get; set; }
        public string? Key { get; set; }
        public string? SASUrl { get; set; }
        public string? ContainerName { get; set; }
        public string? ReviewsContainer { get; set; }
    }
}
