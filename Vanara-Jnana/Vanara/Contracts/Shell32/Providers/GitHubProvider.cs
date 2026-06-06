using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jnana.Vanara.Contracts.Shell32.Providers;

public sealed class GitHubProvider : INamespaceProvider
{
    public string Scheme => "github";

    public bool CanHandle(NamespaceAddress address)
        => address.Raw.Contains('#'); // e.g. "github:vanara#127"

    public async Task<NamespaceNode> ResolveAsync(NamespaceAddress address, CancellationToken ct = default)
    {
        // Parse "vanara#127" → repo + issue
        // TODO: var (repo, number) = GitHubAddressParser.Parse(address);
        // TODO: var issue = await GitHubApi.GetIssueAsync(repo, number, ct);

        //        var node = new NamespaceNode(
        //            address,
        //            displayName: $"#{issue.Number} {issue.Title}",
        //            description: issue.State,
        //            iconKey: "Issue",
        //            payload: issue);

        var node = new NamespaceNode(
            address,
            displayName: $"TODO: {address.Raw}",
            description: "TODO: GitHub Description",
            iconKey: "TODO: GitHub Icon",
            payload: null);

        return node;
    }

    public Task<IReadOnlyList<NamespaceNode>> GetChildrenAsync(NamespaceNode node, CancellationToken ct = default)
    {
        // TODO: Implement
        return Task.FromResult<IReadOnlyList<NamespaceNode>>(Array.Empty<NamespaceNode>());
    }
}
