# Onda 1b — Allegati foto alle note

**Data**: 2026-07-08  
**Status**: Design approved (brainstorm complete)  
**Scope**: Photo attachments as a general capability for all notes (veloce + diario)

---

## 1. Vision

**Goal**: Enable users to attach unlimited photos to any note (veloce or diario) without friction. Photos are stored on disk, visible as thumbnails inline in the note, included in backup/restore, and exported with appropriate formatting per output type.

**Why now**: U7 (Food diary template) depends on photo capability. Designing it early ensures the attachment system is general, testable, and ready for mobile reuse (Fase 5).

**Exit criteria**:
- Photos attachable to veloce and diario notes via file picker
- Thumbnails visible inline; click-to-lightbox
- Included in `.recbak` backup + restore round-trip verified
- Exported to CSV (path), JSON (array), Markdown (![]), HTML (base64), PDF (inline image)
- Full test coverage (Data + WPF)

---

## 2. Architecture

### Layers

```
Recordum.Core
  ├─ AttachmentId (value type, GUID wrapper)
  └─ IAttachmentService (interface)

Recordum.Data
  ├─ Models/Attachment.cs (entity)
  ├─ Services/AttachmentService.cs (CRUD + file I/O)
  ├─ Backup/* (already handles attachments/ folder in .recbak inner ZIP)
  ├─ Export/* (ExportAttachmentRef, formatter extensions)
  └─ Migrations/AttachmentV213.cs

Recordum.WPF
  ├─ ViewModels/Notes/AttachmentViewModel.cs
  ├─ Views/NotesSection/AttachmentGalleryControl.xaml
  ├─ Views/NotesSection/AttachmentLightboxControl.xaml
  └─ Bootstrap/ServiceRegistrar.cs (DI registration)
```

### Key Dependencies

- `IAttachmentService` injected in `NotesSectionViewModel`
- File picker: built-in WinForms `OpenFileDialog` (no NuGet)
- Image rendering: XAML `Image` control + QuestPDF for PDF export
- No new external dependencies

### Storage Layout

```
%LocalAppData%\Recordum\
  ├─ recordum.db
  ├─ attachments/
  │  ├─ {noteId-1}/
  │  │  ├─ {UUID}.jpg
  │  │  └─ {UUID}.png
  │  └─ {noteId-2}/
  │     └─ {UUID}.jpg
  ├─ backups/
  └─ logs/
```

**Rationale**: Per-note organization ensures 1:1 mapping with note soft-delete and makes purge straightforward (delete folder when note is purged).

---

## 3. Data Model

### Entity: `Attachment`

```csharp
public class Attachment
{
    public Guid Id { get; set; }
    public Guid NoteId { get; set; }
    public string Filename { get; set; }      // e.g., "recipe.jpg"
    public string MimeType { get; set; }      // e.g., "image/jpeg"
    public long FileSizeBytes { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public long UpdatedAtMs { get; set; }     // For LWW sync (Fase 6)
    
    public Note Note { get; set; }            // FK (no cascade)
}
```

### Table Schema (v2.13)

```sql
CREATE TABLE attachment (
  id TEXT PRIMARY KEY,
  note_id TEXT NOT NULL,
  filename TEXT NOT NULL,
  mime_type TEXT NOT NULL,
  file_size_bytes INTEGER NOT NULL,
  created_at TEXT NOT NULL,
  updated_at TEXT NOT NULL,
  updated_at_ms INTEGER NOT NULL,
  FOREIGN KEY (note_id) REFERENCES note(id) ON DELETE RESTRICT,
  CHECK (file_size_bytes >= 0)
);
CREATE INDEX ix_attachment_note_id ON attachment(note_id);
```

**Note**: FK has `ON DELETE RESTRICT` (prevents accidental deletion of notes with attachments). Soft-delete via `updated_at` on the note itself; files purged asynchronously (>30 days stale).

### Service Interface (Core)

```csharp
public interface IAttachmentService
{
    /// Add a photo from user-selected file to the attachment folder.
    /// Copies file to %LocalAppData%\Recordum\attachments\{noteId}\{UUID}.{ext}
    /// Returns the persisted Attachment with database row inserted.
    Task<Attachment> AddAsync(Guid noteId, string sourceFilePath, CancellationToken ct);
    
    /// Soft-delete: mark updated_at. File remains on disk for async purge.
    Task DeleteAsync(Guid attachmentId, CancellationToken ct);
    
    /// List all attachments for a note (includes soft-deleted? TBD in plan).
    Task<IReadOnlyList<Attachment>> ListByNoteAsync(Guid noteId, CancellationToken ct);
    
    /// Internal: list all (for backup/export).
    Task<IReadOnlyList<Attachment>> ListAllAsync(CancellationToken ct);
}
```

### Implementation Details (AttachmentService)

1. **AddAsync**:
   - Validate source file exists and is readable
   - Copy to `{appDataPath}\attachments\{noteId}\{Guid.NewGuid():N}.{extension}`
   - Insert row in `attachment` table
   - Return Attachment instance

2. **DeleteAsync**:
   - Soft-delete via `IDbContext.Update(attachment); attachment.UpdatedAt = Clock.UtcNow`
   - File remains (purged asynchronously by background job)

3. **ListByNoteAsync**:
   - Query `SELECT * FROM attachment WHERE note_id = @noteId AND deleted_at IS NULL`
   - (Note: no `deleted_at` column; soft-delete tracked implicitly or via tombstone pattern TBD in plan)

4. **File Purge** (background job, out-of-scope for Onda 1b but noted):
   - Periodically scan `%LocalAppData%\Recordum\attachments/`
   - For each folder `{noteId}`, check if note exists and is not soft-deleted
   - If note is soft-deleted >30 days, delete folder + attachment rows

---

## 4. WPF UI Layer

### AttachmentViewModel

```csharp
public partial class AttachmentViewModel : ObservableObject
{
    public Guid Id { get; set; }
    public Guid NoteId { get; set; }
    
    [ObservableProperty]
    string filename;
    
    [ObservableProperty]
    string mimeType;
    
    [ObservableProperty]
    long fileSizeBytes;
    
    [ObservableProperty]
    string filePath; // Full path to file on disk
    
    [RelayCommand]
    public async Task Delete(IAttachmentService attachmentService, CancellationToken ct)
    {
        await attachmentService.DeleteAsync(Id, ct);
        // Parent (NotesSectionViewModel) observes deletion and updates collection
    }
}
```

### NotesSectionViewModel Changes

```csharp
public partial class NotesSectionViewModel : ObservableObject
{
    [ObservableProperty]
    ObservableCollection<AttachmentViewModel> attachments = new();
    
    [RelayCommand]
    public async Task AddAttachment()
    {
        var picker = new OpenFileDialog
        {
            Filter = "Image files (*.jpg;*.jpeg;*.png;*.gif;*.bmp)|*.jpg;*.jpeg;*.png;*.gif;*.bmp",
            Multiselect = true,
            Title = "Allega foto"
        };
        
        if (picker.ShowDialog() == DialogResult.OK)
        {
            foreach (var path in picker.FileNames)
            {
                var attachment = await _attachmentService.AddAsync(CurrentNoteId, path, ct);
                Attachments.Add(new AttachmentViewModel { /* bind */ });
            }
        }
    }
    
    public async Task LoadAsync(Guid noteId, CancellationToken ct)
    {
        CurrentNoteId = noteId;
        var attachments = await _attachmentService.ListByNoteAsync(noteId, ct);
        Attachments.Clear();
        foreach (var att in attachments)
        {
            Attachments.Add(new AttachmentViewModel { /* ... */ });
        }
    }
}
```

### XAML Gallery Control

```xaml
<Border Margin="0,12,0,0">
  <StackPanel>
    <TextBlock Text="Allegati" Style="{StaticResource Recordum.SectionTitle}" />
    
    <ItemsControl ItemsSource="{Binding Attachments}">
      <ItemsControl.ItemsPanel>
        <ItemsPanelTemplate>
          <WrapPanel />
        </ItemsPanelTemplate>
      </ItemsControl.ItemsPanel>
      <ItemsControl.ItemTemplate>
        <DataTemplate DataType="vmd:AttachmentViewModel">
          <Grid Width="80" Height="80" Margin="4" Background="#F0F0F0">
            <!-- Thumbnail image -->
            <Image Source="{Binding FilePath}" 
                   Stretch="UniformToFill" 
                   Cursor="Hand"
                   MouseLeftButtonDown="OnThumbnailClick" />
            
            <!-- Delete button (top-right corner) -->
            <Button Command="{Binding DeleteCommand}"
                    HorizontalAlignment="Right" 
                    VerticalAlignment="Top"
                    Style="{StaticResource Recordum.IconButtonSmall}"
                    Margin="2">
              ✕
            </Button>
          </Grid>
        </DataTemplate>
      </ItemsControl.ItemTemplate>
    </ItemsControl>
    
    <!-- Add button -->
    <Button Command="{Binding AddAttachmentCommand}"
            Content="+ Allega foto"
            Margin="0,8,0,0" />
  </StackPanel>
</Border>
```

### Lightbox (simple overlay)

A new UserControl `AttachmentLightboxControl.xaml`:
- Shows photo full-screen on a semi-transparent overlay
- Previous/Next buttons if multiple photos
- Close button or click outside to dismiss
- Triggered by clicking a thumbnail

---

## 5. Workflow

### User Attaches a Photo

1. Opens a veloce or diario note
2. Scrolls to "Allegati" section at bottom
3. Clicks "+ Allega foto"
4. File picker dialog opens (Windows standard)
5. Selects one or more JPG/PNG files
6. Each file is:
   - Copied to `%LocalAppData%\Recordum\attachments\{noteId}\{UUID}.jpg`
   - Added to `attachment` table
   - Appears as thumbnail in gallery
7. Clicks thumbnail → lightbox opens (full-screen image, prev/next, close)
8. Clicks ✕ on thumbnail → soft-delete → thumbnail removed from gallery

### Backup & Restore

- **Backup**: `BackupService` already handles `%LocalAppData%\Recordum` directory wholesale → all `attachments/` folders included in inner ZIP, encrypted AES-256-GCM
- **Restore**: `RestoreService` extracts inner ZIP → all files and folders recreated including `attachments/`
- **Round-trip**: attachment files + DB rows fully restored, query-able, visible in UI

### Export

| Format | Treatment |
|--------|-----------|
| **CSV** | Include column `attachments` with semicolon-separated paths, e.g., `attachments/{noteId}/photo1.jpg;attachments/{noteId}/photo2.jpg` |
| **JSON** | Include array `"attachments": [{"filename": "photo1.jpg", "mime": "image/jpeg", "path": "attachments/{noteId}/photo1.jpg"}, ...]` |
| **Markdown** | Embed via `![filename](attachments/{noteId}/photo.jpg)` |
| **HTML** | Embed inline as base64 data URI: `<img src="data:image/jpeg;base64,..." />` |
| **PDF** | Embed via QuestPDF `Image.FromFile()` — images rendered inline within the document |

**Rationale**: CSV/JSON point to files (consumer can fetch locally); Markdown/HTML embed references (portable); PDF embeds actual image data (self-contained).

---

## 6. Testing Strategy

### Data Layer (`Recordum.Data.Tests`)

**AttachmentServiceTests**:
- `AddAsync_copies_file_to_attachments_folder_and_inserts_row`
- `AddAsync_with_multiple_photos_creates_unique_UUIDs`
- `DeleteAsync_soft_deletes_via_updated_at`
- `ListByNoteAsync_returns_all_attachments_for_note`
- `Backup_includes_attachments_folder_in_recbak`
- `Restore_recovers_attachments_from_recbak`
- `Export_csv_includes_attachment_paths`
- `Export_json_includes_attachment_array`
- `Export_markdown_embeds_attachment_refs`
- `Export_html_embeds_base64_images`
- `Export_pdf_embeds_images_via_questpdf`

**Edge cases**:
- File not found when attachment is loaded for export (graceful fallback: skip image or use placeholder)
- Soft-deleted note with attachments (files remain until purge)

### WPF Tests (`Recordum.WPF.Tests`)

**NotesSectionViewModelTests**:
- `LoadAsync_populates_attachments_from_service`
- `AddAttachmentCommand_opens_picker_and_adds_to_collection`
- `DeleteAttachmentCommand_calls_service_and_updates_collection`

**AttachmentGalleryControlSmokeTests**:
- `[WpfFact] Renders_thumbnails_for_each_attachment`
- `[WpfFact] Click_thumbnail_opens_lightbox`
- `[WpfFact] Close_button_dismisses_lightbox`

### Integration Test

- Create note → add 2 photos → save → reload note → verify photos visible → export to all 5 formats → verify paths/embeddings correct

---

## 7. Migration & Schema Versioning

**Migration**: `Recordum.Data/Migrations/20260708000000_AttachmentV213.cs`

- Creates `attachment` table with schema above
- Creates index on `note_id`
- Updates `app_setting` `schema_version` from `2.12` to `2.13`

**Rollback**: Drops table and reverts version.

**Note**: No changes to existing tables; migration is purely additive (safe for mobile reuse in Fase 5).

---

## 8. Constraints & Trade-offs

### Decisions Made

| Decision | Trade-off |
|----------|-----------|
| **Unlimited photos** | Storage grows; mitigated by user responsibility + eventual purge |
| **Per-note folder layout** | Slightly more complex file management; benefit: clean 1:1 with note soft-delete |
| **Files on disk, not DB** | Privacy: files unencrypted; mitigated by .recbak ZIP encryption + file-system ACLs |
| **No auto-compression** | File size user's responsibility; benefit: no CPU cost, preserves quality |
| **WinForms file picker** | Standard Windows UI, no polish; benefit: zero dependencies |
| **Export: paths (not embedded) for CSV/JSON** | Consumer must have access to `attachments/` folder; benefit: reduces export file size |

### Out-of-Scope (Fase 1b)

- Image optimization (resize, compress) — future Onda 3 polish
- Drag-and-drop to add photos — UX future consideration
- Photo cropping/annotation — future Onda 2+
- Cloud sync of attachments — Fase 6 (depends on sync capability)
- Mobile camera capture — Fase 5 (MAUI can access device camera)

---

## 9. Dependencies & Prerequisites

- ✅ Note entity and DB already exist
- ✅ Backup/restore ZIP infrastructure exists (attachments/ folder will be included automatically)
- ✅ Export infrastructure exists (add formatter support for attachments)
- ✅ IAttachmentService interface to be added to Core

**New NuGet**: None (WinForms file picker is built-in .NET).

---

## 10. Success Criteria

1. ✅ User can attach photos to any note via file picker
2. ✅ Photos display as thumbnails inline in the note editor
3. ✅ Click thumbnail opens lightbox (full-screen + prev/next)
4. ✅ Photos included in `.recbak` backup + restore verified
5. ✅ Export includes photos (paths for CSV/JSON, embeds for HTML/PDF/Markdown)
6. ✅ Full test coverage (Data layer CRUD + backup/export; WPF UI rendering + picker)
7. ✅ Schema v2.13 migration applies without errors

---

## 11. Next Steps

Once this design is approved:
1. Write implementation plan (writing-plans skill)
2. Execute 5-8 task plan: Data layer → WPF UI → tests → migration
3. User visual verification (Task 14 equivalent for Onda 1b)
4. Proceed to Onda 2 (per-type experience, which includes U7 Food diary)
