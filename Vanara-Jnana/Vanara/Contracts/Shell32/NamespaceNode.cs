using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jnana.Vanara.Contracts.Shell32;

public sealed class NamespaceNode
{
    public NamespaceAddress Address { get; }
    public string DisplayName { get; }
    public string? Description { get; }
    public string? IconKey { get; } // for glyphs / icons
    public object? Payload { get; } // strongly-typed model (file info, issue, package, clipboard content, ...)

    // PropertyStore for providers to attach arbitrary data (e.g. for caching children)
    public Dictionary<string, object> Properties { get; } = new();

    public NamespaceNode? Parent { get; }

    public NamespaceNode(NamespaceAddress address, string displayName, string? description = null, string? iconKey = null, object? payload = null, NamespaceNode? parent = null)
    {
        Address = address ?? throw new ArgumentNullException(nameof(address));
        DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
        Description = description;
        IconKey = iconKey;
        Payload = payload;
        Parent = parent;
    }
}
