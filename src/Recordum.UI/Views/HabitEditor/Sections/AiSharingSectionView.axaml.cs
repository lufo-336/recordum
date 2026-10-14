using Avalonia.Controls;
using Avalonia.Data.Converters;
using Recordum.Core.Enums;
using Recordum.Presentation.Resources;

namespace Recordum.UI.Views.HabitEditor.Sections;

public partial class AiSharingSectionView : UserControl
{
    public static readonly AiDataSharing[] Levels = Enum.GetValues<AiDataSharing>();

    public static readonly IValueConverter LevelLabel = new FuncValueConverter<AiDataSharing, string>(v => v switch
    {
        AiDataSharing.None => Strings.Editor_AiSharing_None,
        AiDataSharing.LocalOnly => Strings.Editor_AiSharing_LocalOnly,
        AiDataSharing.ApiAllowed => Strings.Editor_AiSharing_ApiAllowed,
        _ => string.Empty,
    });

    public AiSharingSectionView() => InitializeComponent();
}
