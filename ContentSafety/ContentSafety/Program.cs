// See https://aka.ms/new-console-template for more information
using Models;
using Microsoft.Extensions.Configuration;
using System.Text;
using ContentSafety;
using System.Reflection.PortableExecutable;
using Azure.AI.ContentSafety;
using Azure;
using System.Net;

Console.WriteLine("Text safety checker");

IConfigurationRoot config = new ConfigurationBuilder()
    .AddJsonFile("appSettings.json", optional: true)
    .AddUserSecrets<Program>()
    .Build();
var settings = config.GetSection("ContentSafety").Get<Settings>();

if((settings == null) || string.IsNullOrEmpty(settings.Endpoint) || string.IsNullOrEmpty(settings.Key))
{
    Console.WriteLine("Invalid or missing Settings.");
    return;
}

var csc = new ContentSafetyClient(new Uri(settings.Endpoint), new AzureKeyCredential(settings.Key));
// var client = new SafetyChecker(settings.Endpoint, settings.Key);
Response<AnalyzeTextResult> response;
Console.WriteLine("Enter text to check or 'q' to exit.");
do
{
    var inp = Console.ReadLine();
    if ((String.Compare(inp, "q", StringComparison.OrdinalIgnoreCase) == 0) || String.IsNullOrEmpty(inp))
        break;
    try
    {
        response = csc.AnalyzeText(inp);
    }
    catch (RequestFailedException ex)
    {
        Console.WriteLine("Analyze text failed.\nStatus code: {0}, Error code: {1}, Error message: {2}", ex.Status, ex.ErrorCode, ex.Message);
        throw;
    }
    Console.WriteLine("Hate severity: {0}", response.Value.CategoriesAnalysis.FirstOrDefault(a => a.Category == TextCategory.Hate)?.Severity ?? 0);
    Console.WriteLine("SelfHarm severity: {0}", response.Value.CategoriesAnalysis.FirstOrDefault(a => a.Category == TextCategory.SelfHarm)?.Severity ?? 0);
    Console.WriteLine("Sexual severity: {0}", response.Value.CategoriesAnalysis.FirstOrDefault(a => a.Category == TextCategory.Sexual)?.Severity ?? 0);
    Console.WriteLine("Violence severity: {0}", response.Value.CategoriesAnalysis.FirstOrDefault(a => a.Category == TextCategory.Violence)?.Severity ?? 0);

    //var resp = client.IsOKAsync(inp).Result;
    //Console.WriteLine(resp);

} while (true);
