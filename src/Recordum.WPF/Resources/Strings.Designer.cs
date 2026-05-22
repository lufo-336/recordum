#nullable enable
using System.Globalization;
using System.Resources;

namespace Recordum.WPF.Resources;

public static class Strings
{
    private static readonly ResourceManager Manager = new("Recordum.WPF.Resources.Strings", typeof(Strings).Assembly);

    public static CultureInfo? Culture { get; set; }

    // HabitScreen
    public static string HabitScreen_Configure => Get("HabitScreen.Configure");
    public static string HabitScreen_Close => Get("HabitScreen.Close");
    public static string HabitScreen_QuickAdd_Save => Get("HabitScreen.QuickAdd.Save");
    public static string HabitScreen_QuickAdd_Action_Mark => Get("HabitScreen.QuickAdd.Action.Mark");
    public static string HabitScreen_QuickAdd_Date => Get("HabitScreen.QuickAdd.Date");
    public static string HabitScreen_QuickAdd_Value => Get("HabitScreen.QuickAdd.Value");
    public static string HabitScreen_QuickAdd_Notes => Get("HabitScreen.QuickAdd.Notes");
    public static string HabitScreen_QuickAdd_NotesPlaceholder => Get("HabitScreen.QuickAdd.NotesPlaceholder");
    public static string HabitScreen_QuickAdd_AddSeries => Get("HabitScreen.QuickAdd.AddSeries");
    public static string HabitScreen_QuickAdd_RemoveSeries => Get("HabitScreen.QuickAdd.RemoveSeries");
    public static string HabitScreen_QuickAdd_SeriesIndex => Get("HabitScreen.QuickAdd.SeriesIndex");
    public static string HabitScreen_QuickAdd_State_Select => Get("HabitScreen.QuickAdd.State.Select");
    public static string Home_QuickAction => Get("Home.QuickAction");
    public static string HabitScreen_Timer_Start => Get("HabitScreen.Timer.Start");
    public static string HabitScreen_Timer_Pause => Get("HabitScreen.Timer.Pause");
    public static string HabitScreen_Timer_Resume => Get("HabitScreen.Timer.Resume");
    public static string HabitScreen_Timer_Stop => Get("HabitScreen.Timer.Stop");
    public static string HabitScreen_Timer_NextSegment => Get("HabitScreen.Timer.NextSegment");
    public static string QuickAdd_Error_NotesRequired => Get("QuickAdd.Error.NotesRequired");
    public static string QuickAdd_Error_ValueRequired => Get("QuickAdd.Error.ValueRequired");
    public static string HabitScreen_Tab_Completion => Get("HabitScreen.Tab.Completion");
    public static string HabitScreen_Tab_Notes => Get("HabitScreen.Tab.Notes");

    // Notes tab
    public static string Notes_New => Get("Notes.New");
    public static string Notes_Save => Get("Notes.Save");
    public static string Notes_Delete => Get("Notes.Delete");
    public static string Notes_Confirm => Get("Notes.Confirm");
    public static string Notes_Cancel => Get("Notes.Cancel");
    public static string Notes_ConfirmDelete => Get("Notes.ConfirmDelete");
    public static string Notes_Empty => Get("Notes.Empty");
    public static string Notes_EmptyHint => Get("Notes.EmptyHint");
    public static string Notes_Untitled => Get("Notes.Untitled");

    // Editor / WebView2
    public static string Editor_WebView2_NotAvailable_Title => Get("Editor.WebView2.NotAvailable.Title");
    public static string Editor_WebView2_NotAvailable_Body => Get("Editor.WebView2.NotAvailable.Body");

    public static string AppTitle => Get(nameof(AppTitle));
    public static string MainWindowPlaceholder => Get(nameof(MainWindowPlaceholder));
    public static string StartupErrorTitle => Get(nameof(StartupErrorTitle));
    public static string StartupErrorMessage => Get(nameof(StartupErrorMessage));

    // Home screen tabs
    public static string Home_Tab_Active => Get("Home.Tab.Active");
    public static string Home_Tab_Archived => Get("Home.Tab.Archived");
    public static string Home_Tab_Trash => Get("Home.Tab.Trash");

    // Home screen filter / sort
    public static string Home_Filter_SortLabel => Get("Home.Filter.SortLabel");
    public static string Home_Filter_Sort_Manual => Get("Home.Filter.Sort.Manual");
    public static string Home_Filter_Sort_Name => Get("Home.Filter.Sort.Name");
    public static string Home_Filter_Sort_LastCompletion => Get("Home.Filter.Sort.LastCompletion");
    public static string Home_Filter_TypeLabel => Get("Home.Filter.TypeLabel");
    public static string Home_Filter_TagLabel => Get("Home.Filter.TagLabel");
    public static string Home_Filter_HideCompleted => Get("Home.Filter.HideCompleted");
    public static string Home_Filter_SearchPlaceholder => Get("Home.Filter.SearchPlaceholder");

    // Habit type labels
    public static string Home_HabitType_Action => Get("Home.HabitType.Action");
    public static string Home_HabitType_Counter => Get("Home.HabitType.Counter");
    public static string Home_HabitType_Resistance => Get("Home.HabitType.Resistance");
    public static string Home_HabitType_Journal => Get("Home.HabitType.Journal");
    public static string Home_HabitType_State => Get("Home.HabitType.State");

    // Context menu
    public static string Home_ContextMenu_Rename => Get("Home.ContextMenu.Rename");
    public static string Home_ContextMenu_Pin => Get("Home.ContextMenu.Pin");
    public static string Home_ContextMenu_Pin_None => Get("Home.ContextMenu.Pin.None");
    public static string Home_ContextMenu_Pin_Local => Get("Home.ContextMenu.Pin.Local");
    public static string Home_ContextMenu_Pin_Global => Get("Home.ContextMenu.Pin.Global");
    public static string Home_ContextMenu_Archive => Get("Home.ContextMenu.Archive");
    public static string Home_ContextMenu_Unarchive => Get("Home.ContextMenu.Unarchive");
    public static string Home_ContextMenu_SoftDelete => Get("Home.ContextMenu.SoftDelete");
    public static string Home_ContextMenu_Restore => Get("Home.ContextMenu.Restore");
    public static string Home_ContextMenu_Purge => Get("Home.ContextMenu.Purge");

    // Confirmation dialogs
    public static string Home_Confirm_PurgeTitle => Get("Home.Confirm.PurgeTitle");
    public static string Home_Confirm_PurgeMessage => Get("Home.Confirm.PurgeMessage");

    // Loading / empty / error states
    public static string Home_Loading => Get("Home.Loading");
    public static string Home_Empty_Active => Get("Home.Empty.Active");
    public static string Home_Empty_Archived => Get("Home.Empty.Archived");
    public static string Home_Empty_Trash => Get("Home.Empty.Trash");
    public static string Home_Error_Generic => Get("Home.Error.Generic");
    public static string Home_Error_Retry => Get("Home.Error.Retry");
    public static string Home_GroupChildCount_Format => Get("Home.GroupChildCount.Format");

    // Settings
    public static string Settings_Title => Get("Settings.Title");
    public static string Settings_Theme => Get("Settings.Theme");
    public static string Settings_Theme_System => Get("Settings.Theme.System");
    public static string Settings_Theme_Light => Get("Settings.Theme.Light");
    public static string Settings_Theme_Dark => Get("Settings.Theme.Dark");
    public static string Settings_About => Get("Settings.About");
    public static string Settings_Quit => Get("Settings.Quit");

    // Habit Editor (Milestone 3.4)
    public static string Editor_Close => Get("Editor.Close");
    public static string Editor_DescriptionPlaceholder => Get("Editor.DescriptionPlaceholder");
    public static string Editor_Status_Saving => Get("Editor.Status.Saving");
    public static string Editor_Status_Saved => Get("Editor.Status.Saved");
    public static string Editor_Status_Error => Get("Editor.Status.Error");
    public static string Home_NewHabit_Button => Get("Home.NewHabit.Button");
    public static string HabitTemplatePicker_Title => Get("HabitTemplatePicker.Title");
    public static string HabitTemplatePicker_BlankHabit => Get("HabitTemplatePicker.BlankHabit");
    public static string HabitTemplatePicker_BlankHabit_Description => Get("HabitTemplatePicker.BlankHabit.Description");
    public static string HabitTemplatePicker_BlankGroup => Get("HabitTemplatePicker.BlankGroup");
    public static string HabitTemplatePicker_BlankGroup_Description => Get("HabitTemplatePicker.BlankGroup.Description");
    public static string Editor_Error_NameRequired => Get("Editor.Error.NameRequired");
    public static string Editor_Error_NameTooLong => Get("Editor.Error.NameTooLong");
    public static string Editor_Error_DescriptionTooLong => Get("Editor.Error.DescriptionTooLong");
    public static string Editor_Error_ColorInvalid => Get("Editor.Error.ColorInvalid");
    public static string Editor_Error_WeeklyTargetPositive => Get("Editor.Error.WeeklyTargetPositive");
    public static string Editor_Error_EveryXDaysPositive => Get("Editor.Error.EveryXDaysPositive");
    public static string Editor_Error_ToleranceRange => Get("Editor.Error.ToleranceRange");
    public static string Editor_Error_SessionRequiresSegment => Get("Editor.Error.SessionRequiresSegment");
    public static string Editor_Error_TargetNonNegative => Get("Editor.Error.TargetNonNegative");
    public static string Editor_Error_RollingDaysPositive => Get("Editor.Error.RollingDaysPositive");
    public static string Editor_Error_NotificationDuplicate => Get("Editor.Error.NotificationDuplicate");
    public static string Editor_Error_ResetTimeFormat => Get("Editor.Error.ResetTimeFormat");
    public static string Editor_Default_NewHabit => Get("Editor.Default.NewHabit");
    public static string Editor_Default_NewGroup => Get("Editor.Default.NewGroup");
    public static string Editor_Default_NewSegment => Get("Editor.Default.NewSegment");
    public static string Editor_Default_NewMetric => Get("Editor.Default.NewMetric");

    // Detail tab
    public static string HabitScreen_Tab_Detail => Get("HabitScreen.Tab.Detail");
    public static string Detail_GoalCard_NoGoal_Title => Get("Detail.GoalCard.NoGoal.Title");
    public static string Detail_GoalCard_NoGoal_Hint => Get("Detail.GoalCard.NoGoal.Hint");
    public static string Detail_GoalCard_Period_ThisWeek => Get("Detail.GoalCard.Period.ThisWeek");
    public static string Detail_GoalCard_Period_Last30Days => Get("Detail.GoalCard.Period.Last30Days");
    public static string Detail_GoalCard_Period_LastNDays => Get("Detail.GoalCard.Period.LastNDays");
    public static string Detail_GoalCard_Period_AllTime => Get("Detail.GoalCard.Period.AllTime");
    public static string Detail_GoalCard_DaysLeft => Get("Detail.GoalCard.DaysLeft");
    public static string Detail_GoalCard_Done => Get("Detail.GoalCard.Done");
    public static string Detail_Streak_Current => Get("Detail.Streak.Current");
    public static string Detail_Streak_Longest => Get("Detail.Streak.Longest");
    public static string Detail_Streak_Runs90 => Get("Detail.Streak.Runs90");
    public static string Detail_Streak_Empty => Get("Detail.Streak.Empty");
    public static string Detail_Streak_Days => Get("Detail.Streak.Days");
    public static string Detail_Trend_Range_30d => Get("Detail.Trend.Range.30d");
    public static string Detail_Trend_Range_90d => Get("Detail.Trend.Range.90d");
    public static string Detail_Trend_Range_1y => Get("Detail.Trend.Range.1y");
    public static string Detail_Trend_Empty => Get("Detail.Trend.Empty");
    public static string Detail_Hourly_Title => Get("Detail.Hourly.Title");
    public static string Detail_Hourly_Empty => Get("Detail.Hourly.Empty");
    public static string Detail_BasicStreakNotice => Get("Detail.BasicStreakNotice");

    private static string Get(string name) =>
        Manager.GetString(name, Culture) ?? throw new MissingManifestResourceException($"Resource '{name}' not found.");
}
