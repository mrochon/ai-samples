using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;
using static System.Net.WebRequestMethods;

namespace ContentSafety
{
    internal class SafetyChecker
    {
        private HttpClient _httpClient;
        private string _url;
        public SafetyChecker(string endpoint, string key)
        {
            _httpClient = new HttpClient();
            _url = $"{endpoint}/contentsafety";
            _httpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", key);
        }

        public async Task<bool> IsOKAsync(
            string text, 
            IEnumerable<string>? categories = null,
            IEnumerable<string>? blocklistNames = null,
            bool haltOnBlockListHit = true,
            string outputType = "Summary",
            bool pii = true,
            string language = "en")
        {
            var requestObject = new
            {
                text = text,
                categories,
                blocklistNames, // new[] { "AccountNumber", "Address", "PhoneNumber" },
                haltOnBlockListHit,
                outputType, // FourSeverityLevels, Summary, Full
                pii,
                language
            };
            var response = await _httpClient.PostAsync(
                $"{_url}/text:analyze?api-version=2023-10-01",
                JsonContent.Create(requestObject)
            );
            return true;
        }
    }
}
