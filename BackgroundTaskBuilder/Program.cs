// Copyright (c) Microsoft Corporation and Contributors.
// Licensed under the MIT License.

using static Vanara.PInvoke.Ole32;

namespace BackgroundTaskBuilder;

public class Program
{
    static private uint _RegistrationToken;
    static private ManualResetEvent _exitEvent = new ManualResetEvent(false);

    static void Main(string[] args)
    {
        if (args.Contains("-RegisterForBGTaskServer"))
        {
            Guid taskGuid = typeof(BackgroundTask).GUID;
            CoRegisterClassObject(taskGuid,
                new ComServer.BackgroundTaskFactory(),
                CLSCTX.CLSCTX_LOCAL_SERVER,
                REGCLS.REGCLS_MULTIPLEUSE,
                out _RegistrationToken);

            // Wait for the exit event to be signaled before exiting the program
            _exitEvent.WaitOne();
        }
        else
        {
            App.Start(p => new App());
        }
    }

    public static void SignalExit()
    {
        _exitEvent.Set();
    }
}
