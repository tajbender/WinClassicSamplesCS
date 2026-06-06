using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jnana.Vanara.Contracts.Shell32.Providers;

public sealed class Shell32Provider : INamespaceProvider
{
    public string Scheme => "shell32";

    public bool CanHandle(NamespaceAddress address)
        => string.Equals(address.Scheme, Scheme, StringComparison.OrdinalIgnoreCase);

    public Task<NamespaceNode> ResolveAsync(NamespaceAddress address, CancellationToken ct = default)
    {
        // address.Path -> "C:/Windows/Temp"
        // Vanara Shell32 API → File/Folder model
        // TODO: var model = ShellFileSystem.Resolve(address.Path!);

        var node = new NamespaceNode(
            address,
            displayName: "TODO: Shell32 Display Name",
            description: "TODO: Shell32 Description",
            iconKey: "TODO: Shell32 Icon",
            payload: null);

        return Task.FromResult(node);
    }

    public Task<IReadOnlyList<NamespaceNode>> GetChildrenAsync(NamespaceNode node, CancellationToken ct = default)
    {
        // enumerate directory, wrap each as NamespaceNode ...
        return Task.FromResult<IReadOnlyList<NamespaceNode>>(Array.Empty<NamespaceNode>());
    }
}
