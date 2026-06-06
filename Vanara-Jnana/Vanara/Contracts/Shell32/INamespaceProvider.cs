using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jnana.Vanara.Contracts.Shell32;

public interface INamespaceProvider
{
    string Scheme { get; } // "shell32", "https", "nuget", "github", "clipboard", "thought"

    bool CanHandle(NamespaceAddress address);

    Task<NamespaceNode> ResolveAsync(NamespaceAddress address, CancellationToken ct = default);

    // Optional: child navigation (e.g. folder contents, issue comments, etc.)
    Task<IReadOnlyList<NamespaceNode>> GetChildrenAsync(NamespaceNode node, CancellationToken ct = default);
}
