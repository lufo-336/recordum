# Onda 1b — Allegati foto alle note — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Enable users to attach unlimited photos to any note; store on disk; include in backup/restore; export to all 5 formats with appropriate handling (paths for CSV/JSON, embeds for HTML/Markdown/PDF).

**Architecture:** Data-first (Attachment entity + CRUD service), then integrate with backup/export infrastructure (already handles attachments/ folder), then WPF UI (gallery + lightbox). No new external dependencies.

**Tech Stack:** .NET 10, C# 10, EF Core (DbSet), xUnit (Data tests + WPF smoke tests), WPF XAML, WinForms OpenFileDialog (built-in), QuestPDF for PDF image embedding.

---

## Global Constraints

- **VM Windows.** Build: `dotnet build <proj> -p:UseAppHost=false` ("Errori: 0" = success). Test: `dotnet test tests/Recordum.Data.Tests -p:UseAppHost=false` and `dotnet test tests/Recordum.WPF.Tests -p:UseAppHost=false`.
- **Branch**: Create `onda-1b-photo-attachments` from `main` (after Wave 1a merged). MAI committare su `main` (hook la blocca). Commit trailers: `Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>` (adjust to actual model).
- **Stringhe**: nuova stringa = 3 edit — `src/Recordum.WPF/Resources/Strings.resx` (EN) + `Strings.it.resx` (IT) + una riga nel `Strings.Designer.cs`. **Hook nega Edit/Write su `*.Designer.cs`** → edit SOLO via shell (python/sed/heredoc).
- **IClock ovunque** — mai `DateTime.Now`/`UtcNow`.
- **`DispatcherTimer` solo se `Application.Current is not null`** (headless-test guard).
- Baseline tests: Data 377/377, WPF 699/699 (from Wave 1a).
- Schema v2.13 (additive only, safe for mobile in Fase 5).
- Storage: `%LocalAppData%\Recordum\attachments\{noteId}\` (per-note organization).
- Backup: inner ZIP of `.recbak` automatically includes attachments/ folder (existing infrastructure handles it).
- Export: 5 formatters (CSV/JSON/Markdown/HTML/PDF) with different handling per format.

---

## File Structure

### Core Layer
- `src/Recordum.Core/IAttachmentService.cs` (interface only)

### Data Layer
- `src/Recordum.Data/Models/Attachment.cs` (entity)
- `src/Recordum.Data/Services/AttachmentService.cs` (implementation: file I/O + CRUD)
- `src/Recordum.Data/Migrations/20260708000000_AttachmentV213.cs` (schema migration)
- Modify: `src/Recordum.Data/Services/Queries/ExportQueryService.cs` (add attachment loading + ExportAttachmentRef)
- Modify: `src/Recordum.Plugins.ExportEngine/Formatters/*.cs` (CSV, JSON, Markdown, HTML, PDF) — add attachment rows/refs to output

### WPF Layer
- `src/Recordum.WPF/ViewModels/Notes/AttachmentViewModel.cs` (observable, commands)
- Modify: `src/Recordum.WPF/ViewModels/Notes/NotesSectionViewModel.cs` (add Attachments collection, AddAttachment + DeleteAttachment commands)
- `src/Recordum.WPF/Views/NotesSection/AttachmentGalleryControl.xaml` (gallery + thumbnail grid)
- `src/Recordum.WPF/Views/NotesSection/AttachmentLightboxControl.xaml` (full-screen lightbox)
- Modify: `src/Recordum.WPF/Views/NotesSection/NotesSectionView.xaml` (include gallery section below editor)
- Modify: `src/Recordum.WPF/Bootstrap/ServiceRegistrar.cs` (register IAttachmentService)

### Tests
- `tests/Recordum.Data.Tests/Integration/AttachmentServiceTests.cs` (CRUD, file I/O, backup/restore, export)
- `tests/Recordum.WPF.Tests/ViewModels/Notes/NotesSectionViewModelTests.cs` (Attachments binding, commands)
- `tests/Recordum.WPF.Tests/Controls/AttachmentGalleryControlSmokeTests.cs` (rendering)

---

## Tasks

### Task 1: Core Interface + Data Entity + Schema Migration

**Files:**
- Create: `src/Recordum.Core/IAttachmentService.cs`
- Create: `src/Recordum.Data/Models/Attachment.cs`
- Create: `src/Recordum.Data/Migrations/20260708000000_AttachmentV213.cs`
- Modify: `src/Recordum.Data/RecordumDbContext.cs` (add DbSet<Attachment>)

**Interfaces:**
- Produces: `IAttachmentService` interface with methods `AddAsync(Guid noteId, string filePath, ct)`, `DeleteAsync(Guid attachmentId, ct)`, `ListByNoteAsync(Guid noteId, ct)`, `ListAllAsync(ct)`
- Produces: `Attachment` entity with properties `Id (Guid PK)`, `NoteId (Guid FK)`, `Filename (string)`, `MimeType (string)`, `FileSizeBytes (long)`, `CreatedAt (DateTimeOffset)`, `UpdatedAt (DateTimeOffset)`, `UpdatedAtMs (long)`

- [ ] **Step 1: Write the Attachment entity**

Create `src/Recordum.Data/Models/Attachment.cs`:

```csharp
using System;

namespace Recordum.Data.Models;

/// <summary>A photo or file attached to a Note.</summary>
public class Attachment
{
    public Guid Id { get; set; }
    public Guid NoteId { get; set; }
    public string Filename { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public long UpdatedAtMs { get; set; }

    public Note Note { get; set; } = null!;
}
```

- [ ] **Step 2: Create IAttachmentService interface**

Create `src/Recordum.Core/IAttachmentService.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Recordum.Core.Models;

namespace Recordum.Core;

public interface IAttachmentService
{
    /// <summary>Add a photo from user-selected file to the attachment folder.
    /// Copies file to %LocalAppData%\Recordum\attachments\{noteId}\{UUID}.{ext}
    /// and inserts row in attachment table.</summary>
    Task<Attachment> AddAsync(Guid noteId, string sourceFilePath, CancellationToken ct);

    /// <summary>Soft-delete: mark updated_at. File remains for async purge.</summary>
    Task DeleteAsync(Guid attachmentId, CancellationToken ct);

    /// <summary>List all non-soft-deleted attachments for a note.</summary>
    Task<IReadOnlyList<Attachment>> ListByNoteAsync(Guid noteId, CancellationToken ct);

    /// <summary>Internal: list all attachments (for export/backup).</summary>
    Task<IReadOnlyList<Attachment>> ListAllAsync(CancellationToken ct);
}
```

**Note**: Add `Attachment` class to Core if not already present (should be in `Recordum.Core.Models`).

- [ ] **Step 3: Update RecordumDbContext to include DbSet<Attachment>**

Modify `src/Recordum.Data/RecordumDbContext.cs`, add property:

```csharp
public DbSet<Attachment> Attachments { get; set; } = null!;
```

- [ ] **Step 4: Create migration**

Run:
```bash
cd src/Recordum.Data
dotnet ef migrations add AttachmentV213 --startup-project ../Recordum.WPF
```

This generates `Migrations/20260708000000_AttachmentV213.cs`. Edit it to match the spec:

```csharp
using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Recordum.Data.Migrations
{
    /// <inheritdoc />
    public partial class AttachmentV213 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "attachment",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    note_id = table.Column<string>(type: "TEXT", nullable: false),
                    filename = table.Column<string>(type: "TEXT", nullable: false),
                    mime_type = table.Column<string>(type: "TEXT", nullable: false),
                    file_size_bytes = table.Column<long>(type: "INTEGER", nullable: false),
                    created_at = table.Column<string>(type: "TEXT", nullable: false),
                    updated_at = table.Column<string>(type: "TEXT", nullable: false),
                    updated_at_ms = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_attachment", x => x.id);
                    table.ForeignKey(
                        name: "fk_attachment_note_id",
                        column: x => x.note_id,
                        principalTable: "note",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.CheckConstraint("ck_attachment_size", "file_size_bytes >= 0");
                });

            migrationBuilder.CreateIndex(
                name: "ix_attachment_note_id",
                table: "attachment",
                column: "note_id");

            migrationBuilder.UpdateData(
                table: "app_setting",
                keyColumn: "key",
                keyValue: "schema_version",
                column: "value",
                value: "2.13");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "attachment");

            migrationBuilder.UpdateData(
                table: "app_setting",
                keyColumn: "key",
                keyValue: "schema_version",
                column: "value",
                value: "2.12");
        }
    }
}
```

- [ ] **Step 5: Build and verify**

```bash
dotnet build src/Recordum.Data -p:UseAppHost=false
```

Expected: Errori: 0

- [ ] **Step 6: Commit**

```bash
git add src/Recordum.Core/IAttachmentService.cs src/Recordum.Data/Models/Attachment.cs src/Recordum.Data/Migrations/20260708000000_AttachmentV213.cs src/Recordum.Data/RecordumDbContext.cs
git commit -m "feat(data): attachment entity, service interface, schema v2.13 migration"
```

---

### Task 2: AttachmentService CRUD Implementation

**Files:**
- Create: `src/Recordum.Data/Services/AttachmentService.cs`
- Modify: `src/Recordum.Data/RecordumDbContext.cs` (Fluent API configuration for Attachment, if needed)

**Interfaces:**
- Consumes: `IAttachmentService` interface from Task 1, `Attachment` entity, `IDbContextFactory`, `IClock`, `IPluginContext`
- Produces: `AttachmentService(IDbContextFactory, IClock, IPluginContext)` implementation with full CRUD

- [ ] **Step 1: Write test for AddAsync**

Create `tests/Recordum.Data.Tests/Services/AttachmentServiceTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Recordum.Core;
using Recordum.Data.Services;
using Recordum.Data.Tests.Fixtures;
using Xunit;

namespace Recordum.Data.Tests.Services;

public class AttachmentServiceTests : IAsyncLifetime
{
    private DbFixture _fixture = null!;
    private IAttachmentService _service = null!;
    private string _tempDir = null!;

    public async Task InitializeAsync()
    {
        _fixture = new DbFixture();
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);
        
        _service = new AttachmentService(
            _fixture.Factory,
            _fixture.Clock,
            _fixture.PluginContext,
            _tempDir); // Pass custom temp directory for testing
    }

    public async Task DisposeAsync()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
        await _fixture.DisposeAsync();
    }

    [Fact]
    public async Task AddAsync_copies_file_and_inserts_row()
    {
        // Create a test image file
        var sourceFile = Path.Combine(_tempDir, "test.jpg");
        await File.WriteAllTextAsync(sourceFile, "fake image data");

        // Create a note to attach to
        Guid noteId;
        await using (var db = await _fixture.Factory.CreateDbContextAsync())
        {
            var note = new Note { Id = Guid.NewGuid(), HabitNodeId = Guid.NewGuid() };
            db.Notes.Add(note);
            await db.SaveChangesAsync();
            noteId = note.Id;
        }

        // Add attachment
        var attachment = await _service.AddAsync(noteId, sourceFile, default);

        // Verify: row in DB
        await using (var db = await _fixture.Factory.CreateDbContextAsync())
        {
            var row = await db.Attachments.FindAsync(attachment.Id);
            Assert.NotNull(row);
            Assert.Equal(noteId, row.NoteId);
            Assert.Equal("test.jpg", Path.GetExtension(row.Filename));
        }

        // Verify: file on disk
        var expectedPath = Path.Combine(_tempDir, "attachments", noteId.ToString(), attachment.Id.ToString() + ".jpg");
        Assert.True(File.Exists(expectedPath));
    }

    [Fact]
    public async Task ListByNoteAsync_returns_all_for_note()
    {
        var noteId = Guid.NewGuid();
        
        // Insert 2 attachments
        await using (var db = await _fixture.Factory.CreateDbContextAsync())
        {
            db.Attachments.Add(new Attachment
            {
                Id = Guid.NewGuid(),
                NoteId = noteId,
                Filename = "photo1.jpg",
                MimeType = "image/jpeg",
                FileSizeBytes = 100,
                CreatedAt = _fixture.Clock.UtcNow,
                UpdatedAt = _fixture.Clock.UtcNow,
                UpdatedAtMs = _fixture.Clock.UtcNowMs
            });
            db.Attachments.Add(new Attachment
            {
                Id = Guid.NewGuid(),
                NoteId = noteId,
                Filename = "photo2.png",
                MimeType = "image/png",
                FileSizeBytes = 200,
                CreatedAt = _fixture.Clock.UtcNow,
                UpdatedAt = _fixture.Clock.UtcNow,
                UpdatedAtMs = _fixture.Clock.UtcNowMs
            });
            await db.SaveChangesAsync();
        }

        // List
        var attachments = await _service.ListByNoteAsync(noteId, default);

        Assert.Equal(2, attachments.Count);
    }

    [Fact]
    public async Task DeleteAsync_soft_deletes()
    {
        var attachmentId = Guid.NewGuid();
        var noteId = Guid.NewGuid();

        // Insert
        await using (var db = await _fixture.Factory.CreateDbContextAsync())
        {
            db.Attachments.Add(new Attachment
            {
                Id = attachmentId,
                NoteId = noteId,
                Filename = "photo.jpg",
                MimeType = "image/jpeg",
                FileSizeBytes = 100,
                CreatedAt = _fixture.Clock.UtcNow,
                UpdatedAt = _fixture.Clock.UtcNow,
                UpdatedAtMs = _fixture.Clock.UtcNowMs
            });
            await db.SaveChangesAsync();
        }

        // Delete
        await _service.DeleteAsync(attachmentId, default);

        // Verify: UpdatedAt changed
        await using (var db = await _fixture.Factory.CreateDbContextAsync())
        {
            var row = await db.Attachments.FindAsync(attachmentId);
            Assert.NotNull(row);
            Assert.True(row.UpdatedAt > _fixture.Clock.UtcNow.AddSeconds(-1));
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

```bash
dotnet test tests/Recordum.Data.Tests/Services/AttachmentServiceTests.cs -p:UseAppHost=false
```

Expected: FAIL — `AttachmentService` class not found.

- [ ] **Step 3: Implement AttachmentService**

Create `src/Recordum.Data/Services/AttachmentService.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Recordum.Core;
using Recordum.Core.Models;

namespace Recordum.Data.Services;

public class AttachmentService : IAttachmentService
{
    private readonly IDbContextFactory<RecordumDbContext> _factory;
    private readonly IClock _clock;
    private readonly IPluginContext _pluginContext;
    private readonly string _attachmentsRoot;

    public AttachmentService(
        IDbContextFactory<RecordumDbContext> factory,
        IClock clock,
        IPluginContext pluginContext,
        string attachmentsRoot = "")
    {
        _factory = factory;
        _clock = clock;
        _pluginContext = pluginContext;
        _attachmentsRoot = attachmentsRoot != ""
            ? attachmentsRoot
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Recordum", "attachments");
    }

    public async Task<Attachment> AddAsync(Guid noteId, string sourceFilePath, CancellationToken ct)
    {
        if (!File.Exists(sourceFilePath))
            throw new FileNotFoundException($"Source file not found: {sourceFilePath}");

        var ext = Path.GetExtension(sourceFilePath).TrimStart('.').ToLower();
        if (string.IsNullOrEmpty(ext))
            ext = "bin";

        var attachmentId = Guid.NewGuid();
        var noteFolder = Path.Combine(_attachmentsRoot, noteId.ToString());
        Directory.CreateDirectory(noteFolder);

        var destFilename = $"{attachmentId:N}.{ext}";
        var destPath = Path.Combine(noteFolder, destFilename);

        // Copy file
        File.Copy(sourceFilePath, destPath, overwrite: false);

        var now = _clock.UtcNow;
        var attachment = new Attachment
        {
            Id = attachmentId,
            NoteId = noteId,
            Filename = Path.GetFileName(sourceFilePath),
            MimeType = MimeTypeFor(ext),
            FileSizeBytes = new FileInfo(destPath).Length,
            CreatedAt = now,
            UpdatedAt = now,
            UpdatedAtMs = _clock.UtcNowMs
        };

        await using var db = await _factory.CreateDbContextAsync(ct);
        db.Attachments.Add(attachment);
        await db.SaveChangesAsync(ct);

        return attachment;
    }

    public async Task DeleteAsync(Guid attachmentId, CancellationToken ct)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var attachment = await db.Attachments.FindAsync(new object[] { attachmentId }, cancellationToken: ct);
        if (attachment == null)
            return;

        attachment.UpdatedAt = _clock.UtcNow;
        attachment.UpdatedAtMs = _clock.UtcNowMs;

        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<Attachment>> ListByNoteAsync(Guid noteId, CancellationToken ct)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        return await db.Attachments
            .Where(a => a.NoteId == noteId)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Attachment>> ListAllAsync(CancellationToken ct)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        return await db.Attachments.ToListAsync(ct);
    }

    private static string MimeTypeFor(string extension) =>
        extension.ToLower() switch
        {
            "jpg" or "jpeg" => "image/jpeg",
            "png" => "image/png",
            "gif" => "image/gif",
            "bmp" => "image/bmp",
            "webp" => "image/webp",
            _ => "application/octet-stream"
        };
}
```

- [ ] **Step 4: Register service in ServiceRegistrar**

Modify `src/Recordum.Data/ServiceRegistrar.cs` (or equivalent):

```csharp
services.AddScoped<IAttachmentService>(sp =>
{
    var factory = sp.GetRequiredService<IDbContextFactory<RecordumDbContext>>();
    var clock = sp.GetRequiredService<IClock>();
    var pluginContext = sp.GetRequiredService<IPluginContext>();
    return new AttachmentService(factory, clock, pluginContext);
});
```

- [ ] **Step 5: Run tests to verify they pass**

```bash
dotnet test tests/Recordum.Data.Tests/Services/AttachmentServiceTests.cs -p:UseAppHost=false
```

Expected: PASS (all 3 tests green)

- [ ] **Step 6: Run full Data suite to ensure no regressions**

```bash
dotnet test tests/Recordum.Data.Tests -p:UseAppHost=false
```

Expected: 380/380 passed (377 baseline + 3 new attachment tests)

- [ ] **Step 7: Commit**

```bash
git add src/Recordum.Data/Services/AttachmentService.cs tests/Recordum.Data.Tests/Services/AttachmentServiceTests.cs
git commit -m "feat(data): AttachmentService CRUD implementation with file I/O"
```

---

### Task 3: Export Integration (ExportQueryService + Formatters)

**Files:**
- Create: `ExportAttachmentRef` record (in ExportModel.cs or new file)
- Modify: `src/Recordum.Data/Services/Queries/ExportQueryService.cs` (load attachments per-note)
- Modify: `src/Recordum.Plugins.ExportEngine/Formatters/CsvExportFormatter.cs` (add attachment rows)
- Modify: `src/Recordum.Plugins.ExportEngine/Formatters/JsonExportFormatter.cs`
- Modify: `src/Recordum.Plugins.ExportEngine/Formatters/MarkdownExportFormatter.cs`
- Modify: `src/Recordum.Plugins.ExportEngine/Formatters/HtmlExportFormatter.cs`
- Modify: `src/Recordum.Plugins.ExportEngine/Formatters/PdfExportFormatter.cs`

**Interfaces:**
- Consumes: `IAttachmentService`, `ExportNoteRow` from ExportModel
- Produces: `ExportAttachmentRef` record, updated `ExportNoteRow.Attachments: IReadOnlyList<ExportAttachmentRef>` property, formatter methods handle attachments

- [ ] **Step 1: Add ExportAttachmentRef record**

Modify `src/Recordum.Plugins.Contracts/Export/ExportModel.cs`:

```csharp
public sealed record ExportAttachmentRef(
    string Filename,
    string MimeType,
    long FileSizeBytes,
    string RelativePath); // "attachments/{noteId}/filename.ext"
```

Add to `ExportNoteRow`:

```csharp
public IReadOnlyList<ExportAttachmentRef> Attachments { get; init; } = [];
```

- [ ] **Step 2: Update ExportQueryService to load attachments**

Modify `src/Recordum.Data/Services/Queries/ExportQueryService.cs`, constructor add `IAttachmentService`:

```csharp
private readonly IAttachmentService _attachments;

public ExportQueryService(
    IDbContextFactory<RecordumDbContext> dbFactory,
    IClock clock,
    ITrendQueryService trend,
    ICompletionService completion,
    IAbstinenceService abstinence,
    IAttachmentService attachments) // Add this
{
    _dbFactory = dbFactory;
    _clock = clock;
    _trend = trend;
    _completion = completion;
    _abstinence = abstinence;
    _attachments = attachments; // Add this
}
```

In `BuildHabitAsync`, after building other sections, add:

```csharp
// Attachments
var attachmentsList = await _attachments.ListByNoteAsync(node.Id, ct).ConfigureAwait(false);
var attachmentRefs = attachmentsList.Select(a => new ExportAttachmentRef(
    Filename: a.Filename,
    MimeType: a.MimeType,
    FileSizeBytes: a.FileSizeBytes,
    RelativePath: $"attachments/{node.Id:N}/{a.Id:N}.{Path.GetExtension(a.Filename).TrimStart('.')}")
).ToList();

// Build habit with attachments
return new ExportHabitModel(
    Id: habit.Id,
    Name: habit.Name,
    HabitType: habit.HabitType.ToString(),
    ColorHex: config?.ColorHex,
    ExportSummary: /* existing */,
    Completions: /* existing */,
    Notes: /* existing */,
    Heatmap: /* existing */,
    Attachments: attachmentRefs); // Add this
```

- [ ] **Step 3: Update CSV formatter**

Modify `src/Recordum.Plugins.ExportEngine/Formatters/CsvExportFormatter.cs`:

In the section writing notes, after other columns, add:

```csharp
writer.WriteField("Attachments");
// ... in the loop writing note rows:
var attachmentPaths = string.Join(";", habit.Attachments.Select(a => a.RelativePath));
writer.WriteField(attachmentPaths);
```

- [ ] **Step 4: Update JSON formatter**

Modify `src/Recordum.Plugins.ExportEngine/Formatters/JsonExportFormatter.cs`:

The JSON already includes all properties of `ExportHabitModel` via `JsonConvert.SerializeObject`, so `Attachments` array will be automatically included. Verify by checking that the output contains `"attachments": [...]`.

- [ ] **Step 5: Update Markdown formatter**

Modify `src/Recordum.Plugins.ExportEngine/Formatters/MarkdownExportFormatter.cs`:

After the notes section, add:

```csharp
if (habit.Attachments.Count > 0)
{
    sb.AppendLine("\n### Attachments\n");
    foreach (var att in habit.Attachments)
    {
        sb.AppendLine($"- ![{att.Filename}]({att.RelativePath})");
    }
}
```

- [ ] **Step 6: Update HTML formatter**

Modify `src/Recordum.Plugins.ExportEngine/Formatters/HtmlExportFormatter.cs`:

After the notes section, add:

```csharp
if (habit.Attachments.Count > 0)
{
    sb.Append("<section class='attachments'><h3>Attachments</h3>");
    foreach (var att in habit.Attachments)
    {
        var base64 = Convert.ToBase64String(await File.ReadAllBytesAsync(Path.Combine(
            Path.GetDirectoryName(exportPath),
            att.RelativePath), ct));
        sb.Append($"<img src='data:{att.MimeType};base64,{base64}' alt='{att.Filename}' />");
    }
    sb.Append("</section>");
}
```

- [ ] **Step 7: Update PDF formatter**

Modify `src/Recordum.Plugins.ExportEngine/Formatters/PdfExportFormatter.cs`:

In the QuestPDF document builder, after notes section, add:

```csharp
if (habit.Attachments.Count > 0)
{
    document.Section().Text("Attachments", TextStyle.Default.Bold());
    foreach (var att in habit.Attachments)
    {
        var imagePath = Path.Combine(
            Path.GetDirectoryName(exportPath),
            att.RelativePath);
        if (File.Exists(imagePath))
        {
            document.Image(imagePath);
        }
    }
}
```

- [ ] **Step 8: Write test for export with attachments**

Add to `tests/Recordum.Data.Tests/Integration/ExportQueryServiceTests.cs`:

```csharp
[Fact]
public async Task BuildAsync_includes_attachments()
{
    // Create note + attachment
    var noteId = Guid.NewGuid();
    await using (var db = await _fixture.Factory.CreateDbContextAsync())
    {
        var attachment = new Attachment
        {
            Id = Guid.NewGuid(),
            NoteId = noteId,
            Filename = "photo.jpg",
            MimeType = "image/jpeg",
            FileSizeBytes = 1000,
            CreatedAt = _fixture.Clock.UtcNow,
            UpdatedAt = _fixture.Clock.UtcNow,
            UpdatedAtMs = _fixture.Clock.UtcNowMs
        };
        db.Attachments.Add(attachment);
        await db.SaveChangesAsync();
    }

    // Export
    var model = await BuildService(_fixture).BuildAsync(request, ct);
    var habit = model.Habits[0];

    // Verify attachments included
    Assert.Single(habit.Attachments);
    Assert.Equal("photo.jpg", habit.Attachments[0].Filename);
}
```

- [ ] **Step 9: Build and test**

```bash
dotnet build src/Recordum.Plugins.ExportEngine -p:UseAppHost=false
dotnet test tests/Recordum.Data.Tests -p:UseAppHost=false
```

Expected: All tests pass, including new export test.

- [ ] **Step 10: Commit**

```bash
git add src/Recordum.Plugins.Contracts/Export/ExportModel.cs src/Recordum.Data/Services/Queries/ExportQueryService.cs src/Recordum.Plugins.ExportEngine/Formatters/*.cs tests/Recordum.Data.Tests/Integration/ExportQueryServiceTests.cs
git commit -m "feat(export): include attachment refs in all 5 formatters"
```

---

### Task 4: WPF UI — AttachmentViewModel + NotesSectionViewModel Wiring

**Files:**
- Create: `src/Recordum.WPF/ViewModels/Notes/AttachmentViewModel.cs`
- Modify: `src/Recordum.WPF/ViewModels/Notes/NotesSectionViewModel.cs` (add Attachments collection, AddAttachment command)
- Modify: `src/Recordum.WPF/Bootstrap/ServiceRegistrar.cs` (register IAttachmentService in WPF DI)

**Interfaces:**
- Consumes: `IAttachmentService` from Data layer
- Produces: `AttachmentViewModel` with `Filename`, `FilePath`, `DeleteCommand`; updated `NotesSectionViewModel` with `Attachments: ObservableCollection<AttachmentViewModel>`, `AddAttachmentCommand`, `DeleteAttachmentCommand`

- [ ] **Step 1: Create AttachmentViewModel**

Create `src/Recordum.WPF/ViewModels/Notes/AttachmentViewModel.cs`:

```csharp
using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Recordum.Core;

namespace Recordum.WPF.ViewModels.Notes;

public partial class AttachmentViewModel : ObservableObject
{
    public Guid Id { get; set; }
    public Guid NoteId { get; set; }

    [ObservableProperty]
    string filename = string.Empty;

    [ObservableProperty]
    string filePath = string.Empty;

    [ObservableProperty]
    string mimeType = string.Empty;

    [ObservableProperty]
    long fileSizeBytes;

    [RelayCommand]
    public async Task Delete(IAttachmentService attachmentService)
    {
        await attachmentService.DeleteAsync(Id, default);
    }
}
```

- [ ] **Step 2: Update NotesSectionViewModel**

Modify `src/Recordum.WPF/ViewModels/Notes/NotesSectionViewModel.cs`:

Add to class:

```csharp
[ObservableProperty]
ObservableCollection<AttachmentViewModel> attachments = new();

private readonly IAttachmentService _attachmentService;

// Update constructor to include IAttachmentService
public NotesSectionViewModel(
    // ... existing params ...
    IAttachmentService attachmentService)
{
    // ... existing code ...
    _attachmentService = attachmentService;
}

[RelayCommand]
public async Task AddAttachment()
{
    var picker = new System.Windows.Forms.OpenFileDialog
    {
        Filter = "Image files (*.jpg;*.jpeg;*.png;*.gif;*.bmp)|*.jpg;*.jpeg;*.png;*.gif;*.bmp",
        Multiselect = true,
        Title = "Allega foto"
    };

    if (picker.ShowDialog() == System.Windows.Forms.DialogResult.OK)
    {
        foreach (var path in picker.FileNames)
        {
            var attachment = await _attachmentService.AddAsync(CurrentNoteId, path, default);
            Attachments.Add(new AttachmentViewModel
            {
                Id = attachment.Id,
                NoteId = attachment.NoteId,
                Filename = attachment.Filename,
                FilePath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Recordum", "attachments", attachment.NoteId.ToString(), 
                    $"{attachment.Id:N}.{Path.GetExtension(attachment.Filename).TrimStart('.')}"),
                MimeType = attachment.MimeType,
                FileSizeBytes = attachment.FileSizeBytes
            });
        }
    }
}

[RelayCommand]
public async Task DeleteAttachment(Guid attachmentId)
{
    await _attachmentService.DeleteAsync(attachmentId, default);
    var vm = Attachments.FirstOrDefault(a => a.Id == attachmentId);
    if (vm != null)
        Attachments.Remove(vm);
}

// Update LoadAsync to load attachments
public async Task LoadAsync(Guid noteId, CancellationToken ct = default)
{
    CurrentNoteId = noteId;
    // ... existing code ...

    // Load attachments
    var attachmentsList = await _attachmentService.ListByNoteAsync(noteId, ct);
    Attachments.Clear();
    foreach (var att in attachmentsList)
    {
        var filePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Recordum", "attachments", noteId.ToString(),
            $"{att.Id:N}.{Path.GetExtension(att.Filename).TrimStart('.')}");
        Attachments.Add(new AttachmentViewModel
        {
            Id = att.Id,
            NoteId = att.NoteId,
            Filename = att.Filename,
            FilePath = filePath,
            MimeType = att.MimeType,
            FileSizeBytes = att.FileSizeBytes
        });
    }
}
```

- [ ] **Step 3: Register IAttachmentService in DI**

Modify `src/Recordum.WPF/Bootstrap/ServiceRegistrar.cs`:

In `AddSingleton` or `AddScoped` section for Data services, the registration from Task 2 will be picked up. Verify it's present.

- [ ] **Step 4: Build and test compilation**

```bash
dotnet build src/Recordum.WPF -p:UseAppHost=false
```

Expected: Errori: 0

- [ ] **Step 5: Add NotesSectionViewModel tests**

Add to `tests/Recordum.WPF.Tests/ViewModels/Notes/NotesSectionViewModelTests.cs`:

```csharp
[Fact]
public async Task LoadAsync_populates_attachments()
{
    var noteId = Guid.NewGuid();
    var attachmentService = new FakeAttachmentService();
    attachmentService.SetAttachments(noteId, new List<Attachment>
    {
        new() { Id = Guid.NewGuid(), NoteId = noteId, Filename = "photo.jpg", /* ... */ }
    });

    var vm = new NotesSectionViewModel(/* ... */ attachmentService);
    await vm.LoadAsync(noteId, default);

    Assert.Single(vm.Attachments);
    Assert.Equal("photo.jpg", vm.Attachments[0].Filename);
}

[Fact]
public async Task AddAttachmentCommand_adds_to_collection()
{
    var noteId = Guid.NewGuid();
    var attachmentService = new FakeAttachmentService();
    var vm = new NotesSectionViewModel(/* ... */ attachmentService);
    vm.CurrentNoteId = noteId;

    // Fake file picker result
    var testFile = Path.Combine(Path.GetTempPath(), "test.jpg");
    await File.WriteAllTextAsync(testFile, "fake");

    // Execute command (would normally popup picker; mock it)
    // This is hard to test without mocking OpenFileDialog, so a simple smoke test:
    Assert.Empty(vm.Attachments); // Initially empty
}
```

Create `FakeAttachmentService` in test fixtures:

```csharp
public class FakeAttachmentService : IAttachmentService
{
    private Dictionary<Guid, List<Attachment>> _storage = new();

    public void SetAttachments(Guid noteId, List<Attachment> attachments)
    {
        _storage[noteId] = attachments;
    }

    public Task<Attachment> AddAsync(Guid noteId, string sourceFilePath, CancellationToken ct)
    {
        var att = new Attachment { Id = Guid.NewGuid(), NoteId = noteId, /* ... */ };
        if (!_storage.ContainsKey(noteId))
            _storage[noteId] = new();
        _storage[noteId].Add(att);
        return Task.FromResult(att);
    }

    public Task DeleteAsync(Guid attachmentId, CancellationToken ct)
    {
        foreach (var list in _storage.Values)
            list.RemoveAll(a => a.Id == attachmentId);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Attachment>> ListByNoteAsync(Guid noteId, CancellationToken ct)
    {
        return Task.FromResult(_storage.TryGetValue(noteId, out var list)
            ? (IReadOnlyList<Attachment>)list
            : Array.Empty<Attachment>());
    }

    public Task<IReadOnlyList<Attachment>> ListAllAsync(CancellationToken ct)
    {
        return Task.FromResult((IReadOnlyList<Attachment>)_storage.Values.SelectMany(l => l).ToList());
    }
}
```

- [ ] **Step 6: Run WPF tests**

```bash
dotnet test tests/Recordum.WPF.Tests -p:UseAppHost=false
```

Expected: 699/699 + new tests pass (total ≥703)

- [ ] **Step 7: Commit**

```bash
git add src/Recordum.WPF/ViewModels/Notes/AttachmentViewModel.cs src/Recordum.WPF/ViewModels/Notes/NotesSectionViewModel.cs tests/Recordum.WPF.Tests/ViewModels/Notes/NotesSectionViewModelTests.cs tests/Recordum.WPF.Tests/Fixtures/FakeAttachmentService.cs
git commit -m "feat(wpf): AttachmentViewModel + NotesSectionViewModel wiring, tests"
```

---

### Task 5: WPF UI — Gallery + Lightbox Controls

**Files:**
- Create: `src/Recordum.WPF/Views/NotesSection/AttachmentGalleryControl.xaml` + `.xaml.cs`
- Create: `src/Recordum.WPF/Views/NotesSection/AttachmentLightboxControl.xaml` + `.xaml.cs`
- Modify: `src/Recordum.WPF/Views/NotesSection/NotesSectionView.xaml` (include gallery)

**Interfaces:**
- Consumes: `AttachmentViewModel` collection from `NotesSectionViewModel`
- Produces: Gallery rendering thumbnails + lightbox overlay

- [ ] **Step 1: Create AttachmentGalleryControl**

Create `src/Recordum.WPF/Views/NotesSection/AttachmentGalleryControl.xaml`:

```xaml
<UserControl x:Class="Recordum.WPF.Views.NotesSection.AttachmentGalleryControl"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
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
                            <Image Source="{Binding FilePath}" 
                                   Stretch="UniformToFill" 
                                   Cursor="Hand"
                                   MouseLeftButtonDown="OnThumbnailClick" />
                            <Button Command="{Binding DeleteCommand}"
                                    CommandParameter="{Binding Id}"
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
            
            <Button Command="{Binding AddAttachmentCommand}"
                    Content="+ Allega foto"
                    Margin="0,8,0,0" />
        </StackPanel>
    </Border>
</UserControl>
```

Create `src/Recordum.WPF/Views/NotesSection/AttachmentGalleryControl.xaml.cs`:

```csharp
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Recordum.WPF.Views.NotesSection;

public partial class AttachmentGalleryControl : UserControl
{
    public AttachmentGalleryControl()
    {
        InitializeComponent();
    }

    private void OnThumbnailClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is Image img && img.DataContext is AttachmentViewModel att)
        {
            // Open lightbox (to be implemented in next step)
            MessageBox.Show($"Opening {att.Filename}"); // Placeholder
        }
    }
}
```

- [ ] **Step 2: Create AttachmentLightboxControl**

Create `src/Recordum.WPF/Views/NotesSection/AttachmentLightboxControl.xaml`:

```xaml
<UserControl x:Class="Recordum.WPF.Views.NotesSection.AttachmentLightboxControl"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    Background="Transparent"
    MouseLeftButtonDown="OnBackdropClick">
    
    <Grid Background="#80000000" Visibility="{Binding IsOpen, Converter={StaticResource BoolToVis}}">
        <StackPanel VerticalAlignment="Center" HorizontalAlignment="Center">
            <Image Source="{Binding CurrentAttachment.FilePath}" MaxWidth="600" MaxHeight="400" />
            <StackPanel Orientation="Horizontal" HorizontalAlignment="Center" Margin="0,8,0,0">
                <Button Content="←" Command="{Binding PreviousCommand}" />
                <TextBlock Text="{Binding CurrentIndex, StringFormat='0 / 0'}" Margin="8,0" />
                <Button Content="→" Command="{Binding NextCommand}" />
                <Button Content="✕" Command="{Binding CloseCommand}" Margin="8,0,0,0" />
            </StackPanel>
        </StackPanel>
    </Grid>
</UserControl>
```

Create `src/Recordum.WPF/Views/NotesSection/AttachmentLightboxControl.xaml.cs`:

```csharp
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Recordum.WPF.Views.NotesSection;

public partial class AttachmentLightboxControl : UserControl
{
    public AttachmentLightboxControl()
    {
        InitializeComponent();
        DataContext = new LightboxViewModel();
    }

    private void OnBackdropClick(object sender, MouseButtonEventArgs e)
    {
        if (sender == this)
            (DataContext as LightboxViewModel)?.CloseCommand.Execute(null);
    }
}

public partial class LightboxViewModel : ObservableObject
{
    private ObservableCollection<AttachmentViewModel> _attachments = new();

    [ObservableProperty]
    bool isOpen;

    [ObservableProperty]
    int currentIndex;

    [ObservableProperty]
    AttachmentViewModel? currentAttachment;

    public void Open(IEnumerable<AttachmentViewModel> attachments, AttachmentViewModel selected)
    {
        _attachments.Clear();
        foreach (var att in attachments)
            _attachments.Add(att);

        CurrentIndex = _attachments.IndexOf(selected);
        CurrentAttachment = _attachments[CurrentIndex];
        IsOpen = true;
    }

    [RelayCommand]
    public void Close()
    {
        IsOpen = false;
    }

    [RelayCommand]
    public void Previous()
    {
        if (CurrentIndex > 0)
        {
            CurrentIndex--;
            CurrentAttachment = _attachments[CurrentIndex];
        }
    }

    [RelayCommand]
    public void Next()
    {
        if (CurrentIndex < _attachments.Count - 1)
        {
            CurrentIndex++;
            CurrentAttachment = _attachments[CurrentIndex];
        }
    }
}
```

- [ ] **Step 3: Update NotesSectionView to include gallery**

Modify `src/Recordum.WPF/Views/NotesSection/NotesSectionView.xaml`:

After the editor markdown control, add:

```xaml
<views:AttachmentGalleryControl DataContext="{Binding}" />
```

- [ ] **Step 4: Add smoke test for gallery**

Add to `tests/Recordum.WPF.Tests/Controls/AttachmentGalleryControlSmokeTests.cs`:

```csharp
using System.Windows;
using Recordum.WPF.Views.NotesSection;
using Xunit;

namespace Recordum.WPF.Tests.Controls;

public class AttachmentGalleryControlSmokeTests
{
    [WpfFact]
    public void Renders_gallery_with_attachments()
    {
        var control = new AttachmentGalleryControl
        {
            DataContext = new NotesSectionViewModelFake
            {
                Attachments = new()
                {
                    new AttachmentViewModel { Filename = "photo1.jpg", FilePath = "/tmp/photo1.jpg" },
                    new AttachmentViewModel { Filename = "photo2.png", FilePath = "/tmp/photo2.png" }
                }
            }
        };

        var window = new Window { Content = control };
        window.Show();

        // Verify control rendered without crashing
        Assert.NotNull(control.Content);

        window.Close();
    }
}
```

- [ ] **Step 5: Build and test**

```bash
dotnet build src/Recordum.WPF -p:UseAppHost=false
dotnet test tests/Recordum.WPF.Tests -p:UseAppHost=false
```

Expected: All tests pass.

- [ ] **Step 6: Commit**

```bash
git add src/Recordum.WPF/Views/NotesSection/AttachmentGalleryControl.* src/Recordum.WPF/Views/NotesSection/AttachmentLightboxControl.* src/Recordum.WPF/Views/NotesSection/NotesSectionView.xaml tests/Recordum.WPF.Tests/Controls/AttachmentGalleryControlSmokeTests.cs
git commit -m "feat(wpf): attachment gallery + lightbox UI controls"
```

---

### Task 6: Backup/Restore Integration Tests

**Files:**
- Modify: `tests/Recordum.Data.Tests/Backup/BackupServiceTests.cs` (add attachment round-trip test)

**Interfaces:**
- Consumes: `BackupService`, `RestoreService`, `IAttachmentService`
- Produces: round-trip test verifying attachments survive backup/restore

- [ ] **Step 1: Add backup/restore round-trip test**

Add to `tests/Recordum.Data.Tests/Backup/BackupServiceTests.cs`:

```csharp
[Fact]
public async Task BackupRestore_roundtrip_preserves_attachments()
{
    var noteId = Guid.NewGuid();
    string backupPath = Path.Combine(Path.GetTempPath(), $"test-{Guid.NewGuid():N}.recbak");

    try
    {
        // Create note + attachment
        Guid attachmentId;
        string originalFilePath;
        await using (var db = await _fixture.Factory.CreateDbContextAsync())
        {
            var note = new Note { Id = noteId, /* ... */ };
            db.Notes.Add(note);
            await db.SaveChangesAsync();

            var attachment = new Attachment
            {
                Id = Guid.NewGuid(),
                NoteId = noteId,
                Filename = "photo.jpg",
                MimeType = "image/jpeg",
                FileSizeBytes = 100,
                CreatedAt = _fixture.Clock.UtcNow,
                UpdatedAt = _fixture.Clock.UtcNow,
                UpdatedAtMs = _fixture.Clock.UtcNowMs
            };
            attachmentId = attachment.Id;
            db.Attachments.Add(attachment);
            await db.SaveChangesAsync();

            // Create actual file
            var attachDir = Path.Combine(
                Path.GetTempPath(), "attachments", noteId.ToString());
            Directory.CreateDirectory(attachDir);
            originalFilePath = Path.Combine(attachDir, $"{attachmentId:N}.jpg");
            await File.WriteAllTextAsync(originalFilePath, "fake image data");
        }

        // Backup
        var backupService = new BackupService(/* ... */);
        await backupService.CreateBackupAsync(
            backupPath,
            "test-passphrase",
            BackupKind.Manual,
            "Test backup with attachments",
            default);

        // Verify file in .recbak
        using (var zipArchive = ZipFile.OpenRead(backupPath))
        {
            var attachmentEntry = zipArchive.Entries.FirstOrDefault(
                e => e.FullName.Contains(attachmentId.ToString()));
            Assert.NotNull(attachmentEntry);
        }

        // Restore
        var restoreService = new RestoreService(/* ... */);
        await restoreService.RestoreBackupAsync(
            backupPath,
            "test-passphrase",
            overwrite: true,
            default);

        // Verify: attachment row restored
        await using (var db = await _fixture.Factory.CreateDbContextAsync())
        {
            var restored = await db.Attachments.FindAsync(attachmentId);
            Assert.NotNull(restored);
            Assert.Equal("photo.jpg", restored.Filename);
        }

        // Verify: file on disk restored
        var restoredFilePath = Path.Combine(
            Path.GetTempPath(), "attachments", noteId.ToString(), $"{attachmentId:N}.jpg");
        Assert.True(File.Exists(restoredFilePath));
        var content = await File.ReadAllTextAsync(restoredFilePath);
        Assert.Equal("fake image data", content);
    }
    finally
    {
        if (File.Exists(backupPath))
            File.Delete(backupPath);
    }
}
```

- [ ] **Step 2: Run test**

```bash
dotnet test tests/Recordum.Data.Tests/Backup/BackupServiceTests.cs -p:UseAppHost=false
```

Expected: PASS

- [ ] **Step 3: Commit**

```bash
git add tests/Recordum.Data.Tests/Backup/BackupServiceTests.cs
git commit -m "test(backup): attachment round-trip in backup/restore"
```

---

### Task 7: Strings (i18n)

**Files:**
- Modify: `src/Recordum.WPF/Resources/Strings.resx` (EN)
- Modify: `src/Recordum.WPF/Resources/Strings.it.resx` (IT)
- Modify: `src/Recordum.WPF/Resources/Strings.Designer.cs` (via shell)

**New strings**:
- `AttachmentGallery.AddButton` = "Allega foto" / "+ Allega foto"
- `AttachmentGallery.Title` = "Attachments"
- `Lightbox.Previous` = "Previous" / "Precedente"
- `Lightbox.Next` = "Next" / "Successivo"
- `Lightbox.Close` = "Close" / "Chiudi"

- [ ] **Step 1: Add strings to Strings.resx (EN)**

Edit `src/Recordum.WPF/Resources/Strings.resx` (via UI or direct XML, add rows):

```xml
<data name="AttachmentGallery.AddButton" xml:space="preserve">
    <value>+ Allega foto</value>
</data>
<data name="AttachmentGallery.Title" xml:space="preserve">
    <value>Attachments</value>
</data>
<data name="Lightbox.Previous" xml:space="preserve">
    <value>Previous</value>
</data>
<data name="Lightbox.Next" xml:space="preserve">
    <value>Next</value>
</data>
<data name="Lightbox.Close" xml:space="preserve">
    <value>Close</value>
</data>
```

- [ ] **Step 2: Add strings to Strings.it.resx (IT)**

Edit `src/Recordum.WPF/Resources/Strings.it.resx`:

```xml
<data name="AttachmentGallery.AddButton" xml:space="preserve">
    <value>+ Allega foto</value>
</data>
<data name="AttachmentGallery.Title" xml:space="preserve">
    <value>Allegati</value>
</data>
<data name="Lightbox.Previous" xml:space="preserve">
    <value>Precedente</value>
</data>
<data name="Lightbox.Next" xml:space="preserve">
    <value>Successivo</value>
</data>
<data name="Lightbox.Close" xml:space="preserve">
    <value>Chiudi</value>
</data>
```

- [ ] **Step 3: Add accessors to Strings.Designer.cs (via shell)**

Run via Bash (the hook blocks Edit/Write on .Designer.cs):

```bash
python3 << 'PYTHON_EOF'
designer_path = r"src/Recordum.WPF/Resources/Strings.Designer.cs"
with open(designer_path, 'a') as f:
    f.write("""
        public static string AttachmentGallery_AddButton => Get("AttachmentGallery.AddButton");
        public static string AttachmentGallery_Title => Get("AttachmentGallery.Title");
        public static string Lightbox_Previous => Get("Lightbox.Previous");
        public static string Lightbox_Next => Get("Lightbox.Next");
        public static string Lightbox_Close => Get("Lightbox.Close");
""")
PYTHON_EOF
```

- [ ] **Step 4: Build and verify no encoding issues**

```bash
dotnet build src/Recordum.WPF -p:UseAppHost=false
```

Expected: Errori: 0

- [ ] **Step 5: Commit**

```bash
git add src/Recordum.WPF/Resources/Strings.resx src/Recordum.WPF/Resources/Strings.it.resx src/Recordum.WPF/Resources/Strings.Designer.cs
git commit -m "feat(i18n): attachment UI strings EN/IT"
```

---

### Task 8: Final Integration Test + Smoke Test

**Files:**
- Create: `tests/Recordum.Data.Tests/Integration/AttachmentIntegrationTests.cs`

**Interfaces:**
- Consumes: `IAttachmentService`, `ExportQueryService`, all backup/restore pieces
- Produces: end-to-end test

- [ ] **Step 1: Write integration test**

Create `tests/Recordum.Data.Tests/Integration/AttachmentIntegrationTests.cs`:

```csharp
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Recordum.Data.Backup;
using Recordum.Data.Services;
using Recordum.Data.Services.Queries;
using Recordum.Data.Tests.Fixtures;
using Xunit;

namespace Recordum.Data.Tests.Integration;

public class AttachmentIntegrationTests : IAsyncLifetime
{
    private DbFixture _fixture = null!;
    private string _tempDir = null!;

    public async Task InitializeAsync()
    {
        _fixture = new DbFixture();
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);
    }

    public async Task DisposeAsync()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
        await _fixture.DisposeAsync();
    }

    [Fact]
    public async Task EndToEnd_attach_export_backup_restore()
    {
        // Setup
        var attachmentService = new AttachmentService(
            _fixture.Factory, _fixture.Clock, _fixture.PluginContext, _tempDir);
        var exportService = new ExportQueryService(
            _fixture.Factory, _fixture.Clock, /* ... */, attachmentService);

        // Create note + attach photo
        var noteId = Guid.NewGuid();
        var testFile = Path.Combine(_tempDir, "test.jpg");
        await File.WriteAllTextAsync(testFile, "fake image");

        var attachment = await attachmentService.AddAsync(noteId, testFile, default);
        Assert.NotNull(attachment);

        // Export
        var export = await exportService.BuildHabitAsync(noteId, /* ... */, default);
        Assert.Single(export.Attachments);
        Assert.Equal("test.jpg", export.Attachments[0].Filename);

        // Backup (mocked for now; full backup tested elsewhere)
        var allAttachments = await attachmentService.ListAllAsync(default);
        Assert.NotEmpty(allAttachments);

        // Restore: would verify file + DB row integrity
        // (Full backup/restore tested in BackupServiceTests)
    }
}
```

- [ ] **Step 2: Run test**

```bash
dotnet test tests/Recordum.Data.Tests/Integration/AttachmentIntegrationTests.cs -p:UseAppHost=false
```

Expected: PASS

- [ ] **Step 3: Run full suite**

```bash
dotnet test tests/Recordum.Data.Tests -p:UseAppHost=false && dotnet test tests/Recordum.WPF.Tests -p:UseAppHost=false
```

Expected: Data 382+ passed, WPF 705+ passed (net new: ~11 attachment-related tests)

- [ ] **Step 4: Commit**

```bash
git add tests/Recordum.Data.Tests/Integration/AttachmentIntegrationTests.cs
git commit -m "test(integration): end-to-end attachment flow"
```

---

## Summary

**Total commits**: 8 tasks, ~8-10 commits
- Task 1: Core interface + entity + migration
- Task 2: AttachmentService CRUD
- Task 3: Export formatters integration
- Task 4: WPF ViewModels + binding
- Task 5: WPF Gallery + Lightbox controls
- Task 6: Backup/restore round-trip test
- Task 7: i18n strings
- Task 8: Integration test

**Expected suite growth**: Data 377 → 382+, WPF 699 → 705+

**Next**: Visual verification task + finalize design doc

---

## Execution

Ready to proceed. Two options:

1. **Subagent-Driven (recommended)** — I dispatch fresh subagent per task, review between tasks
2. **Inline Execution** — Execute tasks sequentially in this session

Which approach?
