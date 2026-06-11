#nullable enable
using System.Globalization;
using System.Resources;

namespace Recordum.WPF.Resources;

public static class Strings
{
    private static readonly ResourceManager Manager = new("Recordum.WPF.Resources.Strings", typeof(Strings).Assembly);

    public static CultureInfo? Culture { get; set; }

    // Calendar
    public static string Calendar_View_Week => Get("Calendar.View.Week");
    public static string Calendar_View_Month => Get("Calendar.View.Month");
    public static string Calendar_View_Year => Get("Calendar.View.Year");
    public static string Calendar_Year_Months => Get("Calendar.Year.Months");
    public static string Calendar_Year_Heatmap => Get("Calendar.Year.Heatmap");

    // HabitScreen
    public static string HabitScreen_Configure => Get("HabitScreen.Configure");
    public static string HabitScreen_Close => Get("HabitScreen.Close");
    public static string HabitScreen_QuickAdd_Save => Get("HabitScreen.QuickAdd.Save");
    public static string HabitScreen_QuickAdd_Action_Mark => Get("HabitScreen.QuickAdd.Action.Mark");
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
    public static string HabitScreen_Tab_Abitudine => Get("HabitScreen.Tab.Abitudine");
    public static string HabitScreen_Column_Cronologia => Get("HabitScreen.Column.Cronologia");
    public static string HabitScreen_DayRecords => Get("HabitScreen.DayRecords");
    public static string HabitScreen_DayRecords_Empty => Get("HabitScreen.DayRecords.Empty");
    public static string HabitScreen_Tab_Notes => Get("HabitScreen.Tab.Notes");
    public static string HabitScreen_DayNote_Title => Get("HabitScreen.DayNote.Title");
    public static string HabitScreen_DayNote_Save => Get("HabitScreen.DayNote.Save");
    public static string HabitScreen_DayNote_Placeholder => Get("HabitScreen.DayNote.Placeholder");

    // Abitudine tab — selected-day summary strip + QuickAdd label
    public static string Abitudine_DaySummary_Done => Get("Abitudine.DaySummary.Done");
    public static string Abitudine_DaySummary_NotDone => Get("Abitudine.DaySummary.NotDone");
    public static string Abitudine_DaySummary_Notes => Get("Abitudine.DaySummary.Notes");
    public static string Abitudine_QuickAdd_RecordingFor => Get("Abitudine.QuickAdd.RecordingFor");

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
    public static string Notes_Badge_General => Get("Notes.Badge.General");
    public static string Notes_ModifiedAt => Get("Notes_ModifiedAt");
    public static string Notes_History_Current => Get("Notes.History.Current");
    public static string Notes_History => Get("Notes.History");
    public static string Notes_History_Back => Get("Notes.History.Back");
    public static string Notes_History_Restore => Get("Notes.History.Restore");

    // Editor / WebView2
    public static string Editor_WebView2_NotAvailable_Title => Get("Editor.WebView2.NotAvailable.Title");
    public static string Editor_WebView2_NotAvailable_Body => Get("Editor.WebView2.NotAvailable.Body");
    public static string Editor_WebView2_NotAvailable_Download => Get("Editor.WebView2.NotAvailable.Download");

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
    public static string Settings_Category_Appearance => Get("Settings.Category.Appearance");
    public static string Settings_Category_General => Get("Settings.Category.General");
    public static string Settings_Category_Backup => Get("Settings.Category.Backup");
    public static string Settings_Category_Diagnostics => Get("Settings.Category.Diagnostics");
    public static string Settings_Category_Sync => Get("Settings.Category.Sync");
    public static string Settings_Category_Plugins => Get("Settings.Category.Plugins");
    public static string Settings_Category_Ai => Get("Settings.Category.Ai");
    public static string Settings_ComingSoon => Get("Settings.ComingSoon");
    public static string Settings_ComingSoon_Sync => Get("Settings.ComingSoon.Sync");
    public static string Settings_ComingSoon_Plugins => Get("Settings.ComingSoon.Plugins");
    public static string Settings_ComingSoon_Ai => Get("Settings.ComingSoon.Ai");
    public static string Settings_General_DailyResetTime => Get("Settings.General.DailyResetTime");
    public static string Settings_General_DailyResetTime_Hint => Get("Settings.General.DailyResetTime.Hint");

    // Habit Editor (Milestone 3.4)
    public static string Editor_Close => Get("Editor.Close");
    public static string Editor_DescriptionPlaceholder => Get("Editor.DescriptionPlaceholder");
    public static string Editor_Status_Saving => Get("Editor.Status.Saving");
    public static string Editor_Status_Saved => Get("Editor.Status.Saved");
    public static string Editor_Status_Error => Get("Editor.Status.Error");
    public static string Home_NewHabit_Button => Get("Home.NewHabit.Button");
    public static string Home_NewMenu_Habit => Get("Home.NewMenu.Habit");
    public static string Home_NewMenu_Group => Get("Home.NewMenu.Group");
    public static string HabitTemplatePicker_Title => Get("HabitTemplatePicker.Title");
    public static string HabitTemplatePicker_BlankHabit => Get("HabitTemplatePicker.BlankHabit");
    public static string HabitTemplatePicker_BlankHabit_Description => Get("HabitTemplatePicker.BlankHabit.Description");
    public static string HabitTemplatePicker_BlankGroup => Get("HabitTemplatePicker.BlankGroup");
    public static string HabitTemplatePicker_BlankGroup_Description => Get("HabitTemplatePicker.BlankGroup.Description");
    public static string HabitTemplatePicker_FromScratch => Get("HabitTemplatePicker.FromScratch");
    public static string HabitTemplatePicker_YourTemplates => Get("HabitTemplatePicker.YourTemplates");
    public static string HabitTemplatePicker_PredefinedTemplates => Get("HabitTemplatePicker.PredefinedTemplates");
    public static string HabitTemplatePicker_Rename => Get("HabitTemplatePicker.Rename");
    public static string HabitTemplatePicker_Delete => Get("HabitTemplatePicker.Delete");

    // Habit Builder (block-first composer)
    public static string HabitBuilder_Title => Get("HabitBuilder.Title");
    public static string HabitBuilder_SaveAsTemplate => Get("HabitBuilder.SaveAsTemplate");
    public static string HabitBuilder_Launch => Get("HabitBuilder.Launch");
    public static string HabitBuilder_Cancel => Get("HabitBuilder.Cancel");
    public static string HabitBuilder_AddBlock => Get("HabitBuilder.AddBlock");
    public static string HabitBuilder_DerivedType => Get("HabitBuilder.DerivedType");
    public static string HabitBuilder_Name => Get("HabitBuilder.Name");
    public static string HabitBuilder_Color => Get("HabitBuilder.Color");
    public static string HabitBuilder_Icon => Get("HabitBuilder.Icon");
    public static string HabitBuilder_Blocks => Get("HabitBuilder.Blocks");
    public static string HabitBuilder_AddCount => Get("HabitBuilder.AddCount");
    public static string HabitBuilder_AddQuantity => Get("HabitBuilder.AddQuantity");
    public static string HabitBuilder_AddTime => Get("HabitBuilder.AddTime");
    public static string HabitBuilder_AddScale => Get("HabitBuilder.AddScale");
    public static string HabitBuilder_AddText => Get("HabitBuilder.AddText");
    public static string HabitBuilder_Series => Get("HabitBuilder.Series");
    public static string HabitBuilder_SeriesCount => Get("HabitBuilder.SeriesCount");
    public static string HabitBuilder_Preview => Get("HabitBuilder.Preview");
    public static string HabitBuilder_TextFieldNote => Get("HabitBuilder.TextFieldNote");
    public static string HabitBuilder_SeriesRow => Get("HabitBuilder.SeriesRow");
    public static string HabitBuilder_DefaultField_Count => Get("HabitBuilder.DefaultField.Count");
    public static string HabitBuilder_DefaultField_Quantity => Get("HabitBuilder.DefaultField.Quantity");
    public static string HabitBuilder_DefaultField_Time => Get("HabitBuilder.DefaultField.Time");
    public static string HabitBuilder_DefaultField_Scale => Get("HabitBuilder.DefaultField.Scale");
    public static string HabitBuilder_DefaultField_Text => Get("HabitBuilder.DefaultField.Text");
    public static string HabitBuilder_UnitPickerPrompt => Get("HabitBuilder.UnitPickerPrompt");
    public static string HabitBuilder_ScalePickerPrompt => Get("HabitBuilder.ScalePickerPrompt");
    public static string HabitBuilder_LaunchHint => Get("HabitBuilder.LaunchHint");
    public static string HabitBuilder_TemplateSaved => Get("HabitBuilder.TemplateSaved");
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

    // Charts workspace
    public static string Workspace_Home => Get("Workspace.Home");
    public static string Workspace_Charts => Get("Workspace.Charts");
    public static string Charts_New => Get("Charts.New");
    public static string Charts_Delete => Get("Charts.Delete");
    public static string Charts_Empty => Get("Charts.Empty");
    public static string Charts_Builder_Name => Get("Charts.Builder.Name");
    public static string Charts_Builder_Type => Get("Charts.Builder.Type");
    public static string Charts_Builder_Range => Get("Charts.Builder.Range");
    public static string Charts_Builder_Bucket => Get("Charts.Builder.Bucket");
    public static string Charts_Builder_Series => Get("Charts.Builder.Series");
    public static string Charts_Builder_AddSeries => Get("Charts.Builder.AddSeries");
    public static string Charts_Builder_Habit => Get("Charts.Builder.Habit");
    public static string Charts_Builder_Metric => Get("Charts.Builder.Metric");
    public static string Charts_Builder_Color => Get("Charts.Builder.Color");
    public static string Charts_Builder_Save => Get("Charts.Builder.Save");
    public static string Charts_Kind_Line => Get("Charts.Kind.Line");
    public static string Charts_Kind_Bar => Get("Charts.Kind.Bar");
    public static string Charts_Kind_Area => Get("Charts.Kind.Area");
    public static string Charts_Kind_StackedBar => Get("Charts.Kind.StackedBar");
    public static string Charts_Kind_Scatter => Get("Charts.Kind.Scatter");
    public static string Charts_Kind_Pie => Get("Charts.Kind.Pie");

    // Backup / Restore
    public static string Workspace_Backup => Get("Workspace.Backup");
    public static string Backup_Workspace => Get("Backup.Workspace");
    public static string Backup_Menu_Create => Get("Backup.Menu.Create");
    public static string Backup_Menu_Restore => Get("Backup.Menu.Restore");
    public static string Backup_Menu_History => Get("Backup.Menu.History");
    public static string Backup_Menu_Settings => Get("Backup.Menu.Settings");
    public static string Backup_Create_Title => Get("Backup.Create.Title");
    public static string Backup_Create_Path => Get("Backup.Create.Path");
    public static string Backup_Create_Passphrase => Get("Backup.Create.Passphrase");
    public static string Backup_Create_Confirm => Get("Backup.Create.Confirm");
    public static string Backup_Create_Run => Get("Backup.Create.Run");
    public static string Backup_Create_Cancel => Get("Backup.Create.Cancel");
    public static string Backup_Create_Success => Get("Backup.Create.Success");
    public static string Backup_Strength_Weak => Get("Backup.Strength.Weak");
    public static string Backup_Strength_Ok => Get("Backup.Strength.Ok");
    public static string Backup_Strength_Strong => Get("Backup.Strength.Strong");
    public static string Backup_Errors_DestinationNotWritable => Get("Backup.Errors.DestinationNotWritable");
    public static string Backup_Errors_DbBusy => Get("Backup.Errors.DbBusy");
    public static string Backup_Errors_PassphraseMismatch => Get("Backup.Errors.PassphraseMismatch");
    public static string Restore_Title => Get("Restore.Title");
    public static string Restore_Preview => Get("Restore.Preview");
    public static string Restore_Warning => Get("Restore.Warning");
    public static string Restore_Run => Get("Restore.Run");
    public static string Restore_Cancel => Get("Restore.Cancel");
    public static string Restore_Errors_InvalidFormat => Get("Restore.Errors.InvalidFormat");
    public static string Restore_Errors_InvalidPassphrase => Get("Restore.Errors.InvalidPassphrase");
    public static string Restore_Errors_IncompatibleSchema => Get("Restore.Errors.IncompatibleSchema");
    public static string Restore_Errors_SafetySnapshotFailed => Get("Restore.Errors.SafetySnapshotFailed");
    public static string Backup_History_Title => Get("Backup.History.Title");
    public static string Backup_History_Empty => Get("Backup.History.Empty");
    public static string Backup_History_Restore => Get("Backup.History.Restore");
    public static string Backup_History_Delete => Get("Backup.History.Delete");
    public static string Backup_History_OpenFolder => Get("Backup.History.OpenFolder");
    public static string Backup_History_Column_Date => Get("Backup.History.Column.Date");
    public static string Backup_History_Column_Kind => Get("Backup.History.Column.Kind");
    public static string Backup_History_Column_Path => Get("Backup.History.Column.Path");
    public static string Backup_History_Column_Size => Get("Backup.History.Column.Size");
    public static string Backup_History_Column_Schema => Get("Backup.History.Column.Schema");
    public static string Backup_Settings_Title => Get("Backup.Settings.Title");
    public static string Backup_Settings_Enabled => Get("Backup.Settings.Enabled");
    public static string Backup_Settings_Frequency => Get("Backup.Settings.Frequency");
    public static string Backup_Settings_Retention => Get("Backup.Settings.Retention");
    public static string Backup_Settings_Folder => Get("Backup.Settings.Folder");
    public static string Backup_Settings_Test => Get("Backup.Settings.Test");
    public static string Backup_Settings_Save => Get("Backup.Settings.Save");
    public static string Backup_Settings_PassphraseRequired => Get("Backup.Settings.PassphraseRequired");
    public static string Backup_Settings_InertWarning => Get("Backup.Settings.InertWarning");

    // Export
    public static string Export_Title => Get("Export.Title");
    public static string Export_Button => Get("Export.Button");
    public static string Export_Library => Get("Export.Library");
    public static string Export_Format => Get("Export.Format");
    public static string Export_Template => Get("Export.Template");
    public static string Export_Sections => Get("Export.Sections");
    public static string Export_Section_Summary => Get("Export.Section.Summary");
    public static string Export_Section_Heatmap => Get("Export.Section.Heatmap");
    public static string Export_Section_Completions => Get("Export.Section.Completions");
    public static string Export_Section_Notes => Get("Export.Section.Notes");
    public static string Export_Run => Get("Export.Run");
    public static string Export_Cancel => Get("Export.Cancel");

    // Editor section field labels (B9)
    public static string Editor_Field_ScheduleType => Get("Editor.Field.ScheduleType");
    public static string Editor_Field_TimesPerWeek => Get("Editor.Field.TimesPerWeek");
    public static string Editor_Field_EveryXDays => Get("Editor.Field.EveryXDays");
    public static string Editor_Field_StreakMode => Get("Editor.Field.StreakMode");
    public static string Editor_Field_Recovery => Get("Editor.Field.Recovery");
    public static string Editor_Field_Advanced => Get("Editor.Field.Advanced");
    public static string Editor_Field_ToleranceMinutes => Get("Editor.Field.ToleranceMinutes");
    public static string Editor_Action_AddMetric => Get("Editor.Action.AddMetric");
    public static string Editor_Field_Scale => Get("Editor.Field.Scale");
    public static string Editor_Field_CustomLabels => Get("Editor.Field.CustomLabels");
    public static string Editor_Field_IntegratedTimer => Get("Editor.Field.IntegratedTimer");
    public static string Editor_Field_TimerMode => Get("Editor.Field.TimerMode");
    public static string Editor_Field_TargetSeconds => Get("Editor.Field.TargetSeconds");
    public static string Editor_Field_Segments => Get("Editor.Field.Segments");
    public static string Editor_Action_AddSegment => Get("Editor.Action.AddSegment");
    public static string Editor_Field_Rest => Get("Editor.Field.Rest");
    public static string Editor_Field_AutoPrompt => Get("Editor.Field.AutoPrompt");
    public static string Editor_Field_RestTimerSeconds => Get("Editor.Field.RestTimerSeconds");
    public static string Editor_Field_GoalName => Get("Editor.Field.GoalName");
    public static string Editor_Field_Target => Get("Editor.Field.Target");
    public static string Editor_Field_Period => Get("Editor.Field.Period");
    public static string Editor_Field_RollingDays => Get("Editor.Field.RollingDays");
    public static string Editor_Field_AiDataSharing => Get("Editor.Field.AiDataSharing");
    public static string Editor_Field_ResetTime => Get("Editor.Field.ResetTime");
    public static string Editor_Action_AddNotification => Get("Editor.Action.AddNotification");
    public static string Editor_Field_Days => Get("Editor.Field.Days");

    // Editor sections
    public static string Editor_Section_Scheduling => Get("Editor.Section.Scheduling");
    public static string Editor_Section_Streak => Get("Editor.Section.Streak");
    public static string Editor_Section_Metrics => Get("Editor.Section.Metrics");
    public static string Editor_Section_Scales => Get("Editor.Section.Scales");
    public static string Editor_Section_Timer => Get("Editor.Section.Timer");
    public static string Editor_Section_Goal => Get("Editor.Section.Goal");
    public static string Editor_Section_Notifications => Get("Editor.Section.Notifications");
    public static string Editor_Section_Notes => Get("Editor.Section.Notes");
    public static string Editor_Config_NoteColor => Get("Editor.Config.NoteColor");
    public static string Editor_Section_Tags => Get("Editor.Section.Tags");
    public static string Editor_Section_AiSharing => Get("Editor.Section.AiSharing");
    public static string Editor_Section_ResetTime => Get("Editor.Section.ResetTime");

    // Help menu + Diagnostic dialog
    public static string Workspace_Help => Get("Workspace.Help");
    public static string Diagnostic_Menu_Open => Get("Diagnostic.Menu.Open");
    public static string Diagnostic_Title => Get("Diagnostic.Title");
    public static string Diagnostic_Copy => Get("Diagnostic.Copy");
    public static string Diagnostic_Copied => Get("Diagnostic.Copied");
    public static string Diagnostic_Close => Get("Diagnostic.Close");
    public static string Diagnostic_NoBackups => Get("Diagnostic.NoBackups");
    public static string Diagnostic_NotAvailable => Get("Diagnostic.NotAvailable");
    public static string Diagnostic_NotGitCheckout => Get("Diagnostic.NotGitCheckout");

    // Color picker
    public static string ColorPicker_Recent => Get("ColorPicker.Recent");

    // Icon picker
    public static string IconPicker_SearchPrompt => Get("IconPicker.SearchPrompt");

    private static string Get(string name) =>
        Manager.GetString(name, Culture) ?? throw new MissingManifestResourceException($"Resource '{name}' not found.");
}
