using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jnana.Vanara.Contracts.Shell32.Providers;

public sealed class NuGetProvider : INamespaceProvider
{
    public string Scheme => "nuget";

    public bool CanHandle(NamespaceAddress address)
        => !string.IsNullOrEmpty(address.Path);

    public async Task<NamespaceNode> ResolveAsync(NamespaceAddress address, CancellationToken ct = default)
    {
        // "nuget:Vanara.*"
        // TODO: var query = address.Path!;
        // TODO: var packages = await NuGetClient.SearchAsync(query, ct);

        //var node = new NamespaceNode(
        //    address,
        //    displayName: $"NuGet: {query}",
        //    description: $"{packages.Count} packages",
        //    iconKey: "NuGet",
        //    payload: packages);

        var node = new NamespaceNode(
            address,
            displayName: "TODO: NuGet Display Name",
            description: "TODO: NuGet Description",
            iconKey: "TODO: NuGet Icon",
            payload: null);

        return node;
    }

    public Task<IReadOnlyList<NamespaceNode>> GetChildrenAsync(NamespaceNode node, CancellationToken ct = default)
    {
        // TODO: Implement: each package as child node...
        return Task.FromResult<IReadOnlyList<NamespaceNode>>(Array.Empty<NamespaceNode>());
    }
}
