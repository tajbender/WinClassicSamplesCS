using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jnana.Vanara.Contracts.Shell32.Providers;

public sealed class ClipboardProvider : INamespaceProvider
{
    public string Scheme => "clipboard";

    public bool CanHandle(NamespaceAddress address) => true;

    public Task<IReadOnlyList<NamespaceNode>> GetChildrenAsync(NamespaceNode node, CancellationToken ct = default)
    {
        Debug.Print("ClipboardProvider does not support children.");
        return Task.FromResult<IReadOnlyList<NamespaceNode>>(Array.Empty<NamespaceNode>());
    }

    public Task<NamespaceNode> ResolveAsync(NamespaceAddress address, CancellationToken ct = default)
    {
        //// TODO: var content = ClipboardInspector.Read(address); // text/image/filelist

        var node = new NamespaceNode(
            address,
            displayName: "TODO: Clipboard Display Name",
            description: "TODO: Clipboard Description",
            iconKey: "TODO: Clipboard Icon",
            payload: null);

        return Task.FromResult(node);
    }
}
