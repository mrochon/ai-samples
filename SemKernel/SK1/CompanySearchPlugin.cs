using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Globalization;


public class CompanySearchPlugin
{
    [KernelFunction, Description("search employee infomation")]
    public string EmployeeSearch(string input)
    {
        return "talk about hr information";
    }

    [KernelFunction, Description("search weather")]
    public string WeatherSearch(string text)
    {
        return text + ", 2 degree,rainy";
    }
}