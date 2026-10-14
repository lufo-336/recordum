using Avalonia.Controls;
using Recordum.Core.Enums;

namespace Recordum.UI.Views.HabitEditor.Sections;

public partial class AiSharingSectionView : UserControl
{
    public static readonly AiDataSharing[] Levels = Enum.GetValues<AiDataSharing>();

    public AiSharingSectionView() => InitializeComponent();
}
