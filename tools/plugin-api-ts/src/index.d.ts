// @recordum/plugin-api — ambient declarations for the Recordum plugin runtime.
//
// Phase 3 ships TYPES ONLY. The runtime is bound by the Phase 7 WebView2 host.
// First-party consumers (Chart Engine, Export System, Heatmap, etc.) author
// against this contract so their migration path to external plugins is
// boilerplate-only.

export {};

declare global {
  /**
   * The top-level API exposed to every plugin script. All methods are async
   * (Promise-returning) and routed through the host's permission broker.
   */
  const recordum: Recordum.Api;

  // eslint-disable-next-line @typescript-eslint/no-namespace
  namespace Recordum {
    // ---------- Identifiers ----------

    /** Stable identifier of an entity owned by the user. */
    type Id = string;

    /** Reverse-DNS identifier of a plugin (e.g. `io.recordum.chart-engine`). */
    type PluginId = string;

    /** ISO 8601 timestamp. */
    type IsoDate = string;

    /** Unix milliseconds (sync precision). */
    type EpochMs = number;

    // ---------- Permissions ----------

    /** All `data:*` permissions a plugin may request in its manifest. */
    type DataPermission =
      | "read:habit-nodes"
      | "write:habit-nodes"
      | "read:completions"
      | "write:completions"
      | "read:notes"
      | "write:notes"
      | "read:goals"
      | "write:goals"
      | "read:attachments"
      | "read:tags"
      | "write:tags"
      | "read:templates"
      | "read:settings:public";

    type FilesystemAccess = "None" | "PluginStorage" | "AttachmentsRead" | "UserPick";
    type AiAccess = "None" | "Local";

    interface ManifestPermissions {
      data?: DataPermission[];
      network?: string[];
      notifications?: boolean;
      filesystem?: FilesystemAccess;
      ai?: AiAccess;
    }

    // ---------- Extension points ----------

    type ExtensionPointId =
      | "dashboard.widget"
      | "chart.renderer"
      | "habit.detail.tab"
      | "completion.input"
      | "settings.panel"
      | "command-palette"
      | "export.format"
      | "sync.provider"
      | "data.importer";

    interface ExtensionRegistration<TExtension = unknown> {
      pointId: ExtensionPointId;
      id: string;
      extension: TExtension;
    }

    // ---------- Events ----------

    type EventName =
      | "habit.created"
      | "habit.updated"
      | "habit.deleted"
      | "completion.recorded"
      | "completion.updated"
      | "completion.deleted"
      | "note.created"
      | "note.updated"
      | "streak.milestone"
      | "goal.completed"
      | "app.theme.changed"
      | "app.locale.changed";

    interface EventEnvelope<TPayload = unknown> {
      name: EventName;
      payload: TPayload;
      timestamp: IsoDate;
    }

    type EventHandler<TPayload = unknown> = (event: EventEnvelope<TPayload>) => void | Promise<void>;

    interface Subscription {
      unsubscribe(): void;
    }

    // ---------- Domain entities (minimal shapes; full DTOs ship with Phase 7) ----------

    type HabitType = "action" | "counter" | "resistance" | "journal" | "state";

    interface HabitNode {
      id: Id;
      parentId: Id | null;
      name: string;
      nodeType: "habit" | "group";
      habitType?: HabitType;
      color?: string;
      icon?: string;
      createdAt: IsoDate;
      updatedAtMs: EpochMs;
    }

    interface Completion {
      id: Id;
      habitNodeId: Id;
      completedAt: IsoDate;
      completedAtMs: EpochMs;
      values?: ReadonlyArray<CompletionValue>;
    }

    interface CompletionValue {
      metricDefinitionId: Id;
      seriesIndex: number;
      integerValue?: number;
      decimalValue?: number;
      secondsValue?: number;
      textValue?: string;
    }

    interface Note {
      id: Id;
      habitNodeId: Id;
      body: string;
      createdAt: IsoDate;
      updatedAtMs: EpochMs;
    }

    interface Goal {
      id: Id;
      habitNodeId: Id;
      periodType: "fixed" | "rolling" | "open";
      target: number;
      isClosed: boolean;
    }

    // ---------- Manifest (mirrors manifest.schema.json) ----------

    interface PluginManifest {
      schema_version: "1";
      id: PluginId;
      name: string;
      version: string;
      type?: "Plugin" | "Template" | "Theme";
      publisher: { id: string; display_name: string; url?: string };
      min_recordum_version: string;
      description_key?: string;
      icon?: string;
      entry: string;
      permissions?: ManifestPermissions;
      extension_points?: ExtensionPointId[];
      source_url?: string;
      supported_locales?: string[];
    }

    // ---------- Errors ----------

    interface PermissionDeniedError extends Error {
      readonly name: "PermissionDeniedError";
      readonly permission: string;
    }

    interface ValidationError extends Error {
      readonly name: "ValidationError";
      readonly path?: string;
    }

    interface HostUnavailableError extends Error {
      readonly name: "HostUnavailableError";
    }

    // ---------- API surface (Phase 3 contract preview) ----------

    interface Api {
      readonly habits: HabitsApi;
      readonly completions: CompletionsApi;
      readonly notes: NotesApi;
      readonly goals: GoalsApi;
      readonly attachments: AttachmentsApi;
      readonly tags: TagsApi;
      readonly templates: TemplatesApi;
      readonly settings: SettingsApi;
      readonly ui: UiApi;
      readonly events: EventsApi;
      readonly storage: StorageApi;
      readonly i18n: I18nApi;
      readonly network: NetworkApi;
      readonly log: LogApi;
      readonly extensionPoints: ExtensionPointsApi;
    }

    interface HabitsApi {
      get(id: Id): Promise<HabitNode | null>;
      list(filter?: { parentId?: Id | null; archived?: boolean }): Promise<ReadonlyArray<HabitNode>>;
    }

    interface CompletionsApi {
      list(habitNodeId: Id, range?: { from?: IsoDate; to?: IsoDate }): Promise<ReadonlyArray<Completion>>;
      record(input: {
        habitNodeId: Id;
        completedAt?: IsoDate;
        values?: ReadonlyArray<Omit<CompletionValue, "seriesIndex"> & { seriesIndex?: number }>;
      }): Promise<Completion>;
    }

    interface NotesApi {
      list(habitNodeId: Id): Promise<ReadonlyArray<Note>>;
      get(id: Id): Promise<Note | null>;
    }

    interface GoalsApi {
      list(habitNodeId: Id): Promise<ReadonlyArray<Goal>>;
    }

    interface AttachmentsApi {
      get(id: Id): Promise<{ id: Id; mimeType: string; bytes: Uint8Array } | null>;
    }

    interface TagsApi {
      list(): Promise<ReadonlyArray<{ id: Id; name: string }>>;
    }

    interface TemplatesApi {
      list(): Promise<ReadonlyArray<{ id: Id; name: string; kind: "habit" | "export" }>>;
    }

    interface SettingsApi {
      get<TValue = unknown>(key: string): Promise<TValue | undefined>;
    }

    interface UiApi {
      notify(input: { title: string; body?: string }): Promise<void>;
      registerCommand(command: {
        id: string;
        title: string;
        run: () => void | Promise<void>;
      }): Promise<Subscription>;
    }

    interface EventsApi {
      on<TPayload = unknown>(name: EventName, handler: EventHandler<TPayload>): Subscription;
    }

    interface StorageApi {
      get<TValue = unknown>(key: string): Promise<TValue | undefined>;
      set(key: string, value: unknown): Promise<void>;
      remove(key: string): Promise<void>;
    }

    interface I18nApi {
      t(key: string, params?: Record<string, string | number>): string;
      readonly locale: string;
    }

    interface NetworkApi {
      fetch(input: string | URL, init?: RequestInit): Promise<Response>;
    }

    interface LogApi {
      debug(message: string, ...args: unknown[]): void;
      info(message: string, ...args: unknown[]): void;
      warn(message: string, ...args: unknown[]): void;
      error(message: string, ...args: unknown[]): void;
    }

    interface ExtensionPointsApi {
      register<TExtension>(registration: ExtensionRegistration<TExtension>): Promise<Subscription>;
    }
  }
}
