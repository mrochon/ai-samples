using Azure;
using Azure.AI.OpenAI;
using Azure.Core;
using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SharedRAG.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharedRAG.Services
{
    public class BlobService
    {
        private ILogger<BlobService> _logger;
        private IOptions<BlobOptions> _options;
        private readonly BlobContainerClient _client;
        public BlobService(
            ILogger<BlobService> logger,
            IOptions<BlobOptions> options)
        {
            ArgumentNullException.ThrowIfNull(logger);
            ArgumentNullException.ThrowIfNull(options);
            ArgumentNullException.ThrowIfNull(options.Value);
            _logger = logger;
            _options = options;
            // Create an Azure Blob client
            _client = new BlobContainerClient(new Uri(options.Value.SASUrl!));
        }

        public async Task SaveJsonAsync(string name, string blob)
        {
            var blobClient = _client.GetBlobClient(name);
            byte[] contentBytes = Encoding.UTF8.GetBytes(blob);
            using (MemoryStream memoryStream = new MemoryStream(contentBytes))
            {
                var blobOptions = new BlobUploadOptions
                {
                    HttpHeaders = new BlobHttpHeaders
                    {
                        ContentType = "application/json"
                    }
                };
                await blobClient.UploadAsync(memoryStream, blobOptions);
            }
        }
        public async Task<IEnumerable<string>> DownloadBlobsAsync()
        {
            var docs = new List<string>();
            foreach (var blob in _client.GetBlobs())
            {
                Console.WriteLine($"Downloading: {blob.Name}");
                var blobClient = _client.GetBlobClient(blob.Name);
                var response = await blobClient.DownloadAsync();
                using (MemoryStream memoryStream = new MemoryStream())
                {
                    await response.Value.Content.CopyToAsync(memoryStream);
                    docs.Add(Encoding.UTF8.GetString(memoryStream.ToArray()));
                }
            }
            return docs;
        }
    }
}
