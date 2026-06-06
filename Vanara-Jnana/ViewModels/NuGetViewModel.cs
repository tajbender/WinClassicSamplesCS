using ClassicSamplesBrowser.Vanara.NuGet;
//using NuGet.Common;
//using NuGet.Protocol.Core.Types;
using Windows.ApplicationModel;

namespace Jnana.ViewModels;

internal class NuGetViewModel
{
    const string Framework = "net8.0";      // Imported from dahall's code, but not currently used. Consider removing if not needed.
    private const string Prefix = "Vanara"; // The prefix to filter NuGet packages by. This is a simple string match and can be adjusted as needed.
//    readonly List<IPackageSearchMetadata> _packages = [];
//    static readonly ILogger Nuget = NullLogger.Instance; // TODO: Replace with actual nuget if needed
    static readonly CancellationToken CancellationToken = CancellationToken.None;

    public NuGetViewModel()
    {
        Task.Factory.StartNew(async () =>
        {
            //await foreach (var package in NuGetUtils.LoadNuGetPackageListAsync(Prefix, Nuget, CancellationToken))
            //    if (package.Identity.Id.StartsWith(Prefix + '.', StringComparison.OrdinalIgnoreCase))
            //        _packages.Add(package);
        }, CancellationToken);
    }
}
