namespace Recordum.Plugins.Contracts.Permissions;

public static class WellKnownPermissions
{
    // Implicit (always granted)
    public static readonly PermissionId StorageRead = new("storage", "read");
    public static readonly PermissionId StorageWrite = new("storage", "write");
    public static readonly PermissionId CommandsRegister = new("commands", "register");

    // Data (read/write per entity)
    public static readonly PermissionId DataReadHabitNodes = new("data", "read:habit-nodes");
    public static readonly PermissionId DataWriteHabitNodes = new("data", "write:habit-nodes");
    public static readonly PermissionId DataReadCompletions = new("data", "read:completions");
    public static readonly PermissionId DataWriteCompletions = new("data", "write:completions");
    public static readonly PermissionId DataReadNotes = new("data", "read:notes");
    public static readonly PermissionId DataWriteNotes = new("data", "write:notes");
    public static readonly PermissionId DataReadGoals = new("data", "read:goals");
    public static readonly PermissionId DataWriteGoals = new("data", "write:goals");
    public static readonly PermissionId DataReadAttachments = new("data", "read:attachments");
    public static readonly PermissionId DataReadTags = new("data", "read:tags");
    public static readonly PermissionId DataWriteTags = new("data", "write:tags");
    public static readonly PermissionId DataReadTemplates = new("data", "read:templates");
    public static readonly PermissionId DataReadSettingsPublic = new("data", "read:settings:public");

    // AI (local-only inference declaration)
    public static readonly PermissionId AiLocal = new("ai", "local");

    // Network — host whitelist comes from manifest, this is the capability
    public static readonly PermissionId Network = new("network", "*");

    // Notifications, clipboard, filesystem categories
    public static readonly PermissionId NotificationsShow = new("notifications", "show");
    public static readonly PermissionId ClipboardRead = new("clipboard", "read");
    public static readonly PermissionId ClipboardWrite = new("clipboard", "write");
    public static readonly PermissionId FilesystemPluginStorage = new("filesystem", "plugin-storage");
    public static readonly PermissionId FilesystemAttachmentsRead = new("filesystem", "attachments-read");
    public static readonly PermissionId FilesystemUserPick = new("filesystem", "user-pick");
}
