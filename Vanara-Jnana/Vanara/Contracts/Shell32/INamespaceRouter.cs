using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jnana.Vanara.Contracts.Shell32;

public interface INamespaceRouter
{
    void RegisterProvider(INamespaceProvider provider);

    INamespaceProvider? GetProvider(NamespaceAddress address);

    Task<NamespaceNode> NavigateAsync(string input, CancellationToken ct = default);
}
