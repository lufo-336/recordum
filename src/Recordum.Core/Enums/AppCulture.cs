namespace Recordum.Core.Enums;

/// <summary>The UI language and the regional format are chosen from the same short list: the app ships
/// strings in English and Italian only, so offering every installed Windows culture would promise
/// translations that do not exist. <see cref="System"/> follows the machine.</summary>
public enum AppCulture
{
    System = 0,
    English = 1,
    Italian = 2,
}
