using System.Text;
using System.Diagnostics;
using System.Security.Cryptography;

namespace Nettle;

public static class ExtensionMethods
{
    public static void Arguments(this Process Process, params string[] args)
    {
        var Arguments = Process.StartInfo.ArgumentList;

        foreach(string arg in args)
        {
            Arguments.Add(arg);
        }
    }

    public static void Environment(this Process Process, params (string Variable, string Value)[] Variables)
    {
        var Environment = Process.StartInfo.EnvironmentVariables;

        foreach(var (Variable, Value) in Variables)
        {
            Environment[Variable] = Value;
        }
    }

    public static string Sha256(this string Input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(Input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}