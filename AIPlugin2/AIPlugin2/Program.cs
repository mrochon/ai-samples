using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OpenApi;
using System.Net;
using System.Net.Http;
using System.Text.Json.Nodes;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapGet("/.well-known/ai-plugin.json", (HttpRequest req) =>
{
    var json = System.IO.File.ReadAllText("plugin-manifest.json");
    // Parse json string into an object
    var manifest = JsonNode.Parse(json);
    manifest!["api"]!["url"] = $"{req.Scheme}://{req.Host.Value}/swagger/v1/swagger.json";
    manifest!["logo_url"] = $"{req.Scheme}://{req.Host.Value}/logo";
    return manifest;
})
.ExcludeFromDescription();

app.MapGet("/logo", async (ctx) =>
{
    var response = ctx.Response;
    response.Headers.Add("Content-Type", "image/png");
    var logo = await System.IO.File.ReadAllBytesAsync("logo.png");
    response.Body.Write(logo);
})
    .ExcludeFromDescription();

app.MapPost("/destinations", (AirlineAndOrigin crit) =>
{
    return new { airports = new string[] { "LA", "JNB" } };
})
.WithOpenApi(generatedOperation =>
{
    var parameter = generatedOperation.RequestBody;
    parameter.Description = "Airline and origin airport names";
    parameter.Required = true;
    parameter.Content["application/json"].Schema = new Microsoft.OpenApi.Models.OpenApiSchema() { Type = "object" };
    parameter.Content["application/json"].Schema.Properties.Add("airline", new Microsoft.OpenApi.Models.OpenApiSchema() { Description = "Airline name", Title = "Airline id" });
    parameter.Content["application/json"].Schema.Properties.Add("origin", new Microsoft.OpenApi.Models.OpenApiSchema() { Description = "Origin airport" });
    generatedOperation.Responses["200"].Description = "List of destination airports";
    return generatedOperation;
})
.WithSummary("Returns list of destinations.")
.WithDescription("Given airline and airport name, returns list of airports served by that airline from that location.");

app.Run();

record AirlineAndOrigin(string airline, string origin);