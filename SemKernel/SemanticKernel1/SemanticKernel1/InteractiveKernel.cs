using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace SemanticKernel1
{
    public static class InteractiveKernel
    {
        internal static Task<string> GetInputAsync(string v)
        {
            Console.Write(v);
            return Task.FromResult(Console.ReadLine());
        }
        internal static Task<string> GetPasswordAsync(string v)
        {
            Console.Write(v);
            return Task.FromResult(Console.ReadLine());
        }
        internal static string GetClearTextPassword(this string s)
        {
            return s;
        }
    }
}

