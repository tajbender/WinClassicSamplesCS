using System.Threading.Tasks;

namespace WinClassicSamplesBrowser.Activation;

public interface IActivationHandler
{
    bool CanHandle(object args);

    Task HandleAsync(object args);
}
