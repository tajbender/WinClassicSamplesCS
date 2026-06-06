using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jnana.Vanara.Contracts.Shell32;

public sealed class TravelLogEntry
{
    public NamespaceAddress Address { get; }
    public string DisplayName { get; }
    public DateTimeOffset Timestamp { get; }
    public object? Snapshot { get; } // optional serialized payload
}
