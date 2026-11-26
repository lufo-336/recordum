using Avalonia;
using Avalonia.Android;
using Recordum.UI;

namespace Recordum.Android;

// Il processo possiede lock, chiave e contenitore; l'activity riattacca la vista (spec §7.1b).
[global::Android.App.Application(Label = "Recordum", Icon = "@mipmap/ic_launcher")]
public class RecordumAndroidApplication : AvaloniaAndroidApplication<App>
{
    public RecordumAndroidApplication(nint handle, global::Android.Runtime.JniHandleOwnership ownership)
        : base(handle, ownership)
    {
    }

    protected override AppBuilder CreateAppBuilder() =>
        AppBuilder.Configure(() => new App { Head = new AndroidHead() }).UseAndroid();
}
