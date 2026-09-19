namespace Recordum.Presentation.Services;

/// <summary>Lets the user sample a colour from anywhere on screen (eyedropper). Windows-only.</summary>
public interface IScreenColorPicker
{
    /// <returns>A #rrggbb colour, or null if the user cancelled (Esc / right-click).</returns>
    System.Threading.Tasks.Task<string?> PickAsync();
}
