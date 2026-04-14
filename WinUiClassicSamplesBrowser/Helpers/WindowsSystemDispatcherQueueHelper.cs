using Microsoft.UI.Dispatching;

namespace ClassicSamplesBrowser.Helpers;

public class WindowsSystemDispatcherQueueHelper
{
    [StructLayout(LayoutKind.Sequential)]
    struct DispatcherQueueOptions
    {
        public int dwSize;
        public int threadType;
        public int apartmentType;
    }

    [DllImport("CoreMessaging.dll")]
    private static extern int CreateDispatcherQueueController(
        DispatcherQueueOptions options,
        out IntPtr dispatcherQueueController);

    private IntPtr _dispatcherQueueController = IntPtr.Zero;

    public void EnsureWindowsSystemDispatcherQueueController()
    {
        if (DispatcherQueue.GetForCurrentThread() != null)
        {
            // Already initialized
            return;
        }

        if (_dispatcherQueueController == IntPtr.Zero)
        {
            DispatcherQueueOptions options = new()
            {
                dwSize = Marshal.SizeOf(typeof(DispatcherQueueOptions)),
                threadType = 2,      // INFO: TODO: Use Vanara' enum here: `DQTYPE_THREAD_CURRENT`
                apartmentType = 2    // INFO: TODO: Use Vanara' enum here: `DQTAT_COM_STA`
            };

            CreateDispatcherQueueController(options, out _dispatcherQueueController);
        }
    }
}