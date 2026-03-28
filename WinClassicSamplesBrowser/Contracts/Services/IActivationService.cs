using System.Threading.Tasks;

namespace WinClassicSamplesBrowser.Contracts.Services;

public interface IActivationService
{
    Task ActivateAsync(object activationArgs);
}
