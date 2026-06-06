using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jnana.Vanara.Contracts.Shell32;

public sealed class NamespaceRouter : INamespaceRouter
{
    private readonly List<INamespaceProvider> _providers = new();

    public void RegisterProvider(INamespaceProvider provider)
        => _providers.Add(provider);

    public INamespaceProvider? GetProvider(NamespaceAddress address)
        => _providers.FirstOrDefault(p =>
               string.Equals(p.Scheme, address.Scheme, StringComparison.OrdinalIgnoreCase)
               && p.CanHandle(address));

    public async Task<NamespaceNode> NavigateAsync(string input, CancellationToken ct = default)
    {
        var address = NamespaceAddress.Parse(input);
        var provider = GetProvider(address)
            ?? throw new InvalidOperationException($"No provider for scheme '{address.Scheme}'.");

        return await provider.ResolveAsync(address, ct);
    }
}
