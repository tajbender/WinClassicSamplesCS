using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jnana.Vanara.Contracts.Shell32;

public sealed class NamespaceAddress
{
    public string Scheme { get; }
    public string? Host { get; }
    public string? Path { get; }
    public string? Fragment { get; }
    public IReadOnlyDictionary<string, string> Query { get; }

    public string Raw { get; } // original user input

    // TODO: Implement factory: Parse from string ("shell32://C:/Windows", "github:vanara#127", ...)
    public static NamespaceAddress Parse(string input)
    {
        return default; // TODO: placeholder for actual parsing logic. As default, return Shell 32 Desktop namespace address.
    }
}
