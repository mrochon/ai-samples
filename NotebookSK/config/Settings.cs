// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.DotNet.Interactive;
using InteractiveKernel = Microsoft.DotNet.Interactive.Kernel;

// ReSharper disable InconsistentNaming

public static class Settings
{
    private const string DefaultConfigFile = "../config/settings.json";
    private const string TypeKey = "type";
    private const string ModelKey = "model";
    private const string EndpointKey = "endpoint";
    private const string SecretKey = "apikey";
    private const string BingApiKey = "bingKey";
    private const string OrgKey = "org";
    private const string MongoConnKey = "AZURE_MONGODB_CONNECTION";
    private const bool StoreConfigOnFile = true;

    // Prompt user for Azure Endpoint URL
    public static async Task<string> AskAzureEndpoint(bool _useAzureOpenAI = true, string configFile = DefaultConfigFile)
    {
        var (useAzureOpenAI, model, azureEndpoint, apiKey, bingApiKey, orgId) = ReadSettings(_useAzureOpenAI, configFile);

        // If needed prompt user for Azure endpoint
        if (useAzureOpenAI && string.IsNullOrWhiteSpace(azureEndpoint))
        {
            azureEndpoint = await InteractiveKernel.GetInputAsync("Please enter your Azure OpenAI endpoint");
        }

        WriteSettings(configFile, useAzureOpenAI, model, azureEndpoint, apiKey, bingApiKey, orgId);

        // Print report
        if (useAzureOpenAI)
        {
            Console.WriteLine("Settings: " + (string.IsNullOrWhiteSpace(azureEndpoint)
                ? "ERROR: Azure OpenAI endpoint is empty"
                : $"OK: Azure OpenAI endpoint configured [{configFile}]"));
        }

        return azureEndpoint;
    }

    // Prompt user for OpenAI model name / Azure OpenAI deployment name
    public static async Task<string> AskModel(bool _useAzureOpenAI = true, string configFile = DefaultConfigFile)
    {
        var (useAzureOpenAI, model, azureEndpoint, apiKey, bingApiKey, orgId) = ReadSettings(_useAzureOpenAI, configFile);

        // If needed prompt user for model name / deployment name
        if (string.IsNullOrWhiteSpace(model))
        {
            if (useAzureOpenAI)
            {
                model = await InteractiveKernel.GetInputAsync("Please enter your Azure OpenAI deployment name");
            }
            else
            {
                // Use the best model by default, and reduce the setup friction, particularly in VS Studio.
                model = "text-davinci-003";
            }
        }

        WriteSettings(configFile, useAzureOpenAI, model, azureEndpoint, apiKey, bingApiKey, orgId);

        // Print report
        if (useAzureOpenAI)
        {
            Console.WriteLine("Settings: " + (string.IsNullOrWhiteSpace(model)
                ? "ERROR: deployment name is empty"
                : $"OK: deployment name configured [{configFile}]"));
        }
        else
        {
            Console.WriteLine("Settings: " + (string.IsNullOrWhiteSpace(model)
                ? "ERROR: model name is empty"
                : $"OK: AI model configured [{configFile}]"));
        }

        return model;
    }

    // Prompt user for API Key
    public static async Task<string> AskApiKey(bool _useAzureOpenAI = true, string configFile = DefaultConfigFile)
    {
        var (useAzureOpenAI, model, azureEndpoint, apiKey, bingApiKey, orgId) = ReadSettings(_useAzureOpenAI, configFile);

        // If needed prompt user for API key
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            if (useAzureOpenAI)
            {

                apiKey = (await InteractiveKernel.GetPasswordAsync("Please enter your Azure OpenAI API key")).ToString();
                orgId = "";
            }
            else
            {
                apiKey = await InteractiveKernel.GetInputAsync("Please enter your OpenAI API key");
            }
        }

        WriteSettings(configFile, useAzureOpenAI, model, azureEndpoint, apiKey, bingApiKey, orgId);

        // Print report
        Console.WriteLine("Settings: " + (string.IsNullOrWhiteSpace(apiKey)
            ? "ERROR: API key is empty"
            : $"OK: API key configured [{configFile.Replace("settings.", "secrets")}]"));

        return apiKey;
    }

    // Prompt user for OpenAI Organization Id
    public static async Task<string> AskOrg(bool _useAzureOpenAI = true, string configFile = DefaultConfigFile)
    {
        var (useAzureOpenAI, model, azureEndpoint, apiKey, bingApiKey, orgId) = ReadSettings(_useAzureOpenAI, configFile);

        // If needed prompt user for OpenAI Org Id
        if (!useAzureOpenAI && string.IsNullOrWhiteSpace(orgId))
        {
            orgId = await InteractiveKernel.GetInputAsync("Please enter your OpenAI Organization Id (enter 'NONE' to skip)");
        }

        WriteSettings(configFile, useAzureOpenAI, model, azureEndpoint, apiKey, bingApiKey, orgId);

        return orgId;
    }

    // Load settings from file
    public static (bool useAzureOpenAI, string model, string azureEndpoint, string apiKey, string bingApiKey, string orgId)
        LoadFromFile(string configFile = DefaultConfigFile)
    {
        if (!System.IO.File.Exists(DefaultConfigFile))
        {
            Console.WriteLine("Configuration not found: " + DefaultConfigFile);
            Console.WriteLine("\nPlease run the Setup Notebook (0-AI-settings.ipynb) to configure your AI backend first.\n");
            throw new Exception("Configuration not found, please setup the notebooks first using notebook 0-AI-settings.pynb");
        }
        if (!System.IO.File.Exists(DefaultConfigFile.Replace("settings.", "secrets.")))
        {
            Console.WriteLine("Secrets not found: " + DefaultConfigFile.Replace("settings.", "secrets."));
            Console.WriteLine("\nPlease run the Setup Notebook (0-AI-settings.ipynb) to configure your AI backend first.\n");
            throw new Exception("Configuration not found, please setup the notebooks first using notebook 0-AI-settings.pynb");
        }

        try
        {
            var config = JsonSerializer.Deserialize<Dictionary<string, string>>(System.IO.File.ReadAllText(DefaultConfigFile));
            var secrets = JsonSerializer.Deserialize<Dictionary<string, string>>(System.IO.File.ReadAllText(DefaultConfigFile.Replace("settings.", "secrets.")));
            bool useAzureOpenAI = config[TypeKey] == "azure";
            string model = config[ModelKey];
            string azureEndpoint = config[EndpointKey];
            string apiKey = secrets[SecretKey];
            string bingApiKey = secrets[BingApiKey];
            string orgId = config[OrgKey];
            if (orgId == "none") { orgId = ""; }

            return (useAzureOpenAI, model, azureEndpoint, apiKey, bingApiKey, orgId);
        }
        catch (Exception e)
        {
            Console.WriteLine("Something went wrong: " + e.Message);
            return (true, "", "", "", "", "");
        }
    }

    // Delete settings file
    public static void Reset(string configFile = DefaultConfigFile)
    {
        if (!System.IO.File.Exists(configFile)) { return; }

        try
        {
            System.IO.File.Delete(configFile);
            System.IO.File.Delete(configFile.Replace("settings.", "secrets."));
            Console.WriteLine("Settings deleted. Run the notebook again to configure your AI backend.");
        }
        catch (Exception e)
        {
            Console.WriteLine("Something went wrong: " + e.Message);
        }
    }

    // Read and return settings from file
    private static (bool useAzureOpenAI, string model, string azureEndpoint, string apiKey, string bingApiKey, string orgId)
        ReadSettings(bool _useAzureOpenAI, string configFile)
    {
        // Save the preference set in the notebook
        bool useAzureOpenAI = _useAzureOpenAI;
        string model = "";
        string azureEndpoint = "";
        string bingApiKey = "";
        string apiKey = "";
        string orgId = "";

        try
        {
            if (System.IO.File.Exists(configFile))
            {
                (useAzureOpenAI, model, azureEndpoint, apiKey, bingApiKey, orgId) = LoadFromFile(configFile);
            }
        }
        catch (Exception e)
        {
            Console.WriteLine("Something went wrong: " + e.Message);
        }

        // If the preference in the notebook is different from the value on file, then reset
        if (useAzureOpenAI != _useAzureOpenAI)
        {
            Reset(configFile);
            useAzureOpenAI = _useAzureOpenAI;
            model = "";
            azureEndpoint = "";
            apiKey = "";
            orgId = "";
        }

        return (useAzureOpenAI, model, azureEndpoint, apiKey, bingApiKey, orgId);
    }

    public static string MongoDbConnectionString
    {
        get
        {
            var conn = Environment.GetEnvironmentVariable(MongoConnKey);
            if (conn == null) throw new Exception($"{MongoConnKey} environment variable not found");
            return conn;
        }
    }

    // Write settings to file
    private static void WriteSettings(
        string configFile, bool useAzureOpenAI, string model, string azureEndpoint, string apiKey, string bingApiKey, string orgId)
    {
        try
        {
            if (StoreConfigOnFile)
            {
                var data = new Dictionary<string, string>
                {
                    { TypeKey, useAzureOpenAI ? "azure" : "openai" },
                    { ModelKey, model },
                    { EndpointKey, azureEndpoint },
                    //{ SecretKey, apiKey },
                    { OrgKey, orgId },
                };
                var secretData = new Dictionary<string, string>
                {
                    { SecretKey, apiKey },
                    { BingApiKey, bingApiKey }
                };

                var options = new JsonSerializerOptions { WriteIndented = true };
                System.IO.File.WriteAllText(configFile, JsonSerializer.Serialize(data, options));
                System.IO.File.WriteAllText(configFile.Replace("settings.", "secrets."), JsonSerializer.Serialize(secretData, options));
            }
        }
        catch (Exception e)
        {
            Console.WriteLine("Something went wrong: " + e.Message);
        }

        // If asked then delete the credentials stored on disk
        if (!StoreConfigOnFile && System.IO.File.Exists(configFile))
        {
            try
            {
                System.IO.File.Delete(configFile);
            }
            catch (Exception e)
            {
                Console.WriteLine("Something went wrong: " + e.Message);
            }
        }
    }
}