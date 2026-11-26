using Avalonia.Android;

namespace Recordum.Android;

[global::Android.App.Activity(
    Name = "io.github.lufo336.recordum.MainActivity",
    Label = "Recordum",
    Theme = "@style/RecordumTheme",
    MainLauncher = true,
    Exported = true,
    LaunchMode = global::Android.Content.PM.LaunchMode.SingleTop,
    ConfigurationChanges =
        global::Android.Content.PM.ConfigChanges.Orientation |
        global::Android.Content.PM.ConfigChanges.ScreenSize |
        global::Android.Content.PM.ConfigChanges.UiMode |
        global::Android.Content.PM.ConfigChanges.ScreenLayout |
        global::Android.Content.PM.ConfigChanges.SmallestScreenSize)]
public class MainActivity : AvaloniaMainActivity
{
    private static int _creations;

    protected override void OnCreate(global::Android.OS.Bundle? savedInstanceState)
    {
        if (Interlocked.Increment(ref _creations) > 1)
        {
            // Task 5: PhoneStartupHost.RebuildShell()
        }

        base.OnCreate(savedInstanceState);
    }

    // Indietro non gestito alla radice: AvaloniaActivity chiama Finish() e chiuderebbe l'activity (sonda 1).
    public override void Finish() => _ = MoveTaskToBack(true);
}
