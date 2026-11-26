using Recordum.Core.Abstractions;

namespace Recordum.Android;

public sealed class AndroidApplicationLifetime : IApplicationLifetime
{
    public void Shutdown()
    {
        // Dopo un ripristino il processo deve ripartire da zero; Android lo riavvia al prossimo tocco.
        global::Android.OS.Process.KillProcess(global::Android.OS.Process.MyPid());
    }
}
