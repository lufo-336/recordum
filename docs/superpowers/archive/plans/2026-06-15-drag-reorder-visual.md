# Drag-and-drop visivo + riordino manuale — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Dare al drag-and-drop dell'albero Home un fantasma agganciato al cursore, indicatori di posizione (linea sopra/sotto + highlight gruppo) e un riordino manuale persistente su `HabitNode.SortOrder`.

**Architecture:** Lo stato visivo passa da flag sul ViewModel (`IsBeingDragged`, `DropEdge`, `IsDropTarget` esistente), restando unit-testabile. La persistenza dell'ordine vive in un unico metodo Data anchor-based (`MoveAndReorderAsync`) riusato sia da Before/After sia da Onto (append). Il ghost è un `Adorner` WPF dedicato, smontato deterministicamente perché `DragDrop.DoDragDrop` è bloccante.

**Tech Stack:** C# / .NET 10, WPF, CommunityToolkit.Mvvm, EF Core + SQLite, xUnit + FluentAssertions.

**Spec:** `docs/superpowers/archive/specs/2026-06-15-drag-reorder-visual-design.md`. Refinement rispetto alla spec: il riordino è **anchor-based** (`anchorSiblingId` + `placeAfter`) invece di un `targetIndex` intero — più robusto rispetto alle differenze tra vista filtrata e ordine DB. Semantica invariata.

---

## File Structure

| File | Responsabilità | Azione |
|---|---|---|
| `src/Recordum.Core/Abstractions/Services/IHabitNodeService.cs` | Aggiunge `MoveAndReorderAsync` | Modify |
| `src/Recordum.Data/Services/HabitNodeService.cs` | Implementa riordino transazionale + cycle guard | Modify |
| `tests/Recordum.Data.Tests/Integration/HabitNodeServiceTests.cs` | Test riordino Data | Modify |
| `tests/Recordum.WPF.Tests/Fixtures/FakeHabitNodeService.cs` | Registra le chiamate di riordino | Modify |
| `src/Recordum.Core/Abstractions/Services/Dtos/HomeTreeRow.cs` | Trasporta `SortOrder` | Modify |
| `src/Recordum.Data/Services/Queries/HabitTreeQueryService.cs` | Proietta `SortOrder` | Modify |
| `src/Recordum.WPF/ViewModels/HomeViewModel.cs` | `ApplySort` Manual ordina per SortOrder; `ApplyDropAsync` con auto-switch | Modify |
| `src/Recordum.WPF/ViewModels/RowDropEdge.cs` | Enum `None/Before/After` | Create |
| `src/Recordum.WPF/ViewModels/HomeRowViewModel.cs` | Flag `IsBeingDragged`, `DropEdge` | Modify |
| `src/Recordum.WPF/Behaviors/TreeDragDropBehavior.cs` | `ResolveDrop` puro + indicatore singolo + spawn adorner + dispatch | Modify |
| `src/Recordum.WPF/Behaviors/DragAdorner.cs` | Ghost che segue il cursore | Create |
| `tests/Recordum.WPF.Tests/Behaviors/TreeDragDropBehaviorTests.cs` | Test `ResolveDrop` | Modify |
| `tests/Recordum.WPF.Tests/ViewModels/HomeMultiSelectionTests.cs` o nuovo file | Test `ApplyDropAsync` | Modify/Create |
| `src/Recordum.WPF/App.xaml` | Trigger `IsBeingDragged` + `DropEdge` nel template `Recordum.HabitBlock` | Modify |

---

## Task 1: Servizio Data — `MoveAndReorderAsync`

Riordina i fratelli sotto un genitore inserendo il nodo spostato accanto a un'ancora, riassegnando `SortOrder` contigui. Anchor `null` = append in coda.

**Files:**
- Modify: `src/Recordum.Core/Abstractions/Services/IHabitNodeService.cs`
- Modify: `src/Recordum.Data/Services/HabitNodeService.cs`
- Modify: `tests/Recordum.Data.Tests/Integration/HabitNodeServiceTests.cs`
- Modify: `tests/Recordum.WPF.Tests/Fixtures/FakeHabitNodeService.cs`

- [ ] **Step 1: Add the interface method**

In `IHabitNodeService.cs`, dopo `MoveAsync` (riga 10):

```csharp
    /// <summary>
    /// Moves <paramref name="id"/> under <paramref name="newParentId"/> and reorders it among its
    /// siblings: placed before/after <paramref name="anchorSiblingId"/> (or appended at the end when
    /// the anchor is null). Reassigns contiguous SortOrder (0..n-1) to the sibling set. Transactional.
    /// </summary>
    ValueTask MoveAndReorderAsync(
        Guid id, Guid? newParentId, Guid? anchorSiblingId, bool placeAfter, CancellationToken ct = default);
```

- [ ] **Step 2: Write the failing Data tests**

In `tests/Recordum.Data.Tests/Integration/HabitNodeServiceTests.cs`, prima della chiusura della classe:

```csharp
    [Fact]
    public async Task Reorder_after_anchor_reassigns_contiguous_sort_order()
    {
        await using var fx = new DbFixture();
        var svc = Svc(fx);
        var g = await svc.CreateAsync(new HabitNode { NodeType = NodeType.Group, Name = "g" });
        var a = await svc.CreateAsync(new HabitNode { NodeType = NodeType.Habit, HabitType = HabitType.Action, Name = "a", ParentId = g, SortOrder = 0 });
        var b = await svc.CreateAsync(new HabitNode { NodeType = NodeType.Habit, HabitType = HabitType.Action, Name = "b", ParentId = g, SortOrder = 1 });
        var c = await svc.CreateAsync(new HabitNode { NodeType = NodeType.Habit, HabitType = HabitType.Action, Name = "c", ParentId = g, SortOrder = 2 });

        // Move c to sit right after a → order should become a, c, b.
        await svc.MoveAndReorderAsync(c, g, anchorSiblingId: a, placeAfter: true);

        await using var db = await fx.Factory.CreateDbContextAsync();
        var ordered = await db.HabitNodes.Where(n => n.ParentId == g)
            .OrderBy(n => n.SortOrder).Select(n => n.Name).ToListAsync();
        ordered.Should().Equal("a", "c", "b");
        (await db.HabitNodes.Where(n => n.ParentId == g).Select(n => n.SortOrder).OrderBy(x => x).ToListAsync())
            .Should().Equal(0, 1, 2);
    }

    [Fact]
    public async Task Reorder_with_null_anchor_appends_at_end_and_can_change_parent()
    {
        await using var fx = new DbFixture();
        var svc = Svc(fx);
        var g1 = await svc.CreateAsync(new HabitNode { NodeType = NodeType.Group, Name = "g1" });
        var g2 = await svc.CreateAsync(new HabitNode { NodeType = NodeType.Group, Name = "g2" });
        var x = await svc.CreateAsync(new HabitNode { NodeType = NodeType.Habit, HabitType = HabitType.Action, Name = "x", ParentId = g2, SortOrder = 0 });
        var leaf = await svc.CreateAsync(new HabitNode { NodeType = NodeType.Habit, HabitType = HabitType.Action, Name = "leaf", ParentId = g1, SortOrder = 0 });

        await svc.MoveAndReorderAsync(leaf, g2, anchorSiblingId: null, placeAfter: false);

        await using var db = await fx.Factory.CreateDbContextAsync();
        var moved = (await db.HabitNodes.FindAsync(leaf))!;
        moved.ParentId.Should().Be(g2);
        moved.SortOrder.Should().Be(1); // appended after x
    }

    [Fact]
    public async Task Reorder_into_own_descendant_throws_CyclicHabitNodeMoveException()
    {
        await using var fx = new DbFixture();
        var svc = Svc(fx);
        var root = await svc.CreateAsync(new HabitNode { NodeType = NodeType.Group, Name = "root" });
        var child = await svc.CreateAsync(new HabitNode { NodeType = NodeType.Group, Name = "child", ParentId = root });
        await Assert.ThrowsAsync<CyclicHabitNodeMoveException>(() =>
            svc.MoveAndReorderAsync(root, child, anchorSiblingId: null, placeAfter: false).AsTask());
    }
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test tests/Recordum.Data.Tests/Recordum.Data.Tests.csproj --filter "FullyQualifiedName~Reorder"`
Expected: FAIL to compile / `MoveAndReorderAsync` not found on `HabitNodeService`.

- [ ] **Step 4: Implement `MoveAndReorderAsync`**

In `src/Recordum.Data/Services/HabitNodeService.cs`, aggiungi un logger message dopo `LogMoved` (riga 25):

```csharp
    [LoggerMessage(EventId = 8, Level = LogLevel.Information, Message = "HabitNode {Id} reordered under {NewParent} (anchor {Anchor}, after={After})")]
    private partial void LogReordered(Guid id, Guid? newParent, Guid? anchor, bool after);
```

e il metodo subito dopo `MoveAsync` (dopo riga 79):

```csharp
    public async ValueTask MoveAndReorderAsync(
        Guid id, Guid? newParentId, Guid? anchorSiblingId, bool placeAfter, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var node = await db.HabitNodes.FirstOrDefaultAsync(n => n.Id == id, ct)
            ?? throw new HabitNodeNotFoundException(id);
        if (node.DeletedAt is not null) throw new HabitNodeAlreadySoftDeletedException(id);

        if (newParentId is { } np)
        {
            var descendantIds = await DescendantIdsAsync(db, id, ct);
            if (np == id || descendantIds.Contains(np))
                throw new CyclicHabitNodeMoveException(id, np);
            var parentExists = await db.HabitNodes.AnyAsync(n => n.Id == np, ct);
            if (!parentExists) throw new HabitNodeNotFoundException(np);
        }

        node.ParentId = newParentId;

        // Build the sibling sequence under the (new) parent, excluding the moved node,
        // in current SortOrder then Name order for stability.
        var siblings = await db.HabitNodes
            .Where(n => n.ParentId == newParentId && n.Id != id && n.DeletedAt == null)
            .OrderBy(n => n.SortOrder).ThenBy(n => n.Name)
            .ToListAsync(ct);

        var insertAt = siblings.Count; // default: append
        if (anchorSiblingId is { } anchor)
        {
            var anchorIndex = siblings.FindIndex(n => n.Id == anchor);
            if (anchorIndex >= 0) insertAt = placeAfter ? anchorIndex + 1 : anchorIndex;
        }

        siblings.Insert(insertAt, node);
        for (var i = 0; i < siblings.Count; i++) siblings[i].SortOrder = i;

        await db.SaveChangesAsync(ct);
        LogReordered(id, newParentId, anchorSiblingId, placeAfter);
    }
```

> Nota: `node` è già tracciato dal contesto; aggiungerlo a `siblings` (lista in memoria) e iterare assegna `SortOrder` anche al nodo spostato. `DescendantIdsAsync` esiste già (usato da `MoveAsync`).

- [ ] **Step 5: Implement the fake for WPF tests**

In `tests/Recordum.WPF.Tests/Fixtures/FakeHabitNodeService.cs`, dopo `_moveCalls` (riga 22):

```csharp
    private readonly List<(Guid Id, Guid? NewParent, Guid? Anchor, bool After)> _reorderCalls = new();
    public IReadOnlyList<(Guid Id, Guid? NewParent, Guid? Anchor, bool After)> ReorderCalls => _reorderCalls;
```

e dopo `MoveAsync` (riga 55):

```csharp
    public ValueTask MoveAndReorderAsync(
        Guid id, Guid? newParentId, Guid? anchorSiblingId, bool placeAfter, CancellationToken ct = default)
    {
        Throw();
        _reorderCalls.Add((id, newParentId, anchorSiblingId, placeAfter));
        if (Store.TryGetValue(id, out var n)) n.ParentId = newParentId;
        return ValueTask.CompletedTask;
    }
```

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test tests/Recordum.Data.Tests/Recordum.Data.Tests.csproj --filter "FullyQualifiedName~Reorder"`
Expected: PASS (3 test).

- [ ] **Step 7: Commit**

```bash
git add src/Recordum.Core/Abstractions/Services/IHabitNodeService.cs src/Recordum.Data/Services/HabitNodeService.cs tests/Recordum.Data.Tests/Integration/HabitNodeServiceTests.cs tests/Recordum.WPF.Tests/Fixtures/FakeHabitNodeService.cs
git commit -m "feat(data): MoveAndReorderAsync — anchor-based sibling reorder on SortOrder"
```

---

## Task 2: `SortOrder` nelle righe + `HomeSort.Manual` attivo

**Files:**
- Modify: `src/Recordum.Core/Abstractions/Services/Dtos/HomeTreeRow.cs`
- Modify: `src/Recordum.Data/Services/Queries/HabitTreeQueryService.cs`
- Modify: `src/Recordum.WPF/ViewModels/HomeViewModel.cs:438-443`
- Test: `tests/Recordum.WPF.Tests/ViewModels/` (nuovo file `HomeSortManualTests.cs`)

- [ ] **Step 1: Add `SortOrder` to the DTO**

In `HomeTreeRow.cs`, aggiungi un parametro **prima** dei parametri opzionali (`Icon` resta ultimo, con default):

```csharp
public sealed record HomeTreeRow(
    Guid HabitNodeId,
    Guid? ParentId,
    string Name,
    string ColorHex,
    HabitType? HabitType,
    DateTime? LastCompletionAt,
    int CurrentStreakCount,
    bool IsCompletedToday,
    PinScope PinScope,
    int ChildCount,
    bool IsArchived,
    ImmutableArray<int> CompletionsLast7Days,
    ImmutableArray<Guid> TagIds,
    int SortOrder,
    string? Icon = null);
```

- [ ] **Step 2: Project `SortOrder` in the query**

In `HabitTreeQueryService.cs`: nel blocco `.Select(n => new { ... })` (riga 68-85) aggiungi `n.SortOrder,` dopo `n.Name,`; poi nella costruzione di `HomeTreeRow` (riga 88-102) aggiungi `SortOrder: r.SortOrder,` prima di `Icon: r.Icon`:

```csharp
                n.ParentId,
                n.Name,
                n.SortOrder,
                n.Color,
```
```csharp
                TagIds: tagsByHabit.TryGetValue(r.Id, out var tids) ? tids : ImmutableArray<Guid>.Empty,
                SortOrder: r.SortOrder,
                Icon: r.Icon))
```

- [ ] **Step 3: Write the failing test for Manual sort**

Crea `tests/Recordum.WPF.Tests/ViewModels/HomeSortManualTests.cs`. Usa la stessa factory delle altre suite Home. Cerca un test Home esistente (`HomeMultiSelectionTests.cs`) per il pattern di costruzione del `HomeViewModel` e del fake tree-query, e replica il setup minimo. Il test crea due root con `SortOrder` 1 e 0 e verifica che in `HomeSort.Manual` la riga con SortOrder 0 venga prima:

```csharp
using FluentAssertions;
using Recordum.Core.Enums;
using Recordum.WPF.ViewModels;
using Xunit;

namespace Recordum.WPF.Tests.ViewModels;

public class HomeSortManualTests
{
    [Fact]
    public async Task Manual_sort_orders_roots_by_sort_order()
    {
        // ARRANGE: replicate the Home setup used in HomeMultiSelectionTests, seeding two
        // root rows: "second" with SortOrder 1, "first" with SortOrder 0 (insertion order
        // deliberately reversed so a no-op sort would fail).
        var sut = HomeTestHarness.Build(rows: new[]
        {
            HomeTestHarness.Root("second", sortOrder: 1),
            HomeTestHarness.Root("first",  sortOrder: 0),
        });
        await sut.LoadCommand.ExecuteAsync(null);

        // ACT
        sut.SortOption = HomeSort.Manual;

        // ASSERT
        sut.Rows.Select(r => r.Name).Should().Equal("first", "second");
    }
}
```

> Se non esiste già un harness condiviso, estrai il setup ripetuto di `HomeMultiSelectionTests` in un piccolo `HomeTestHarness` statico (metodi `Build(rows)` e `Root(name, sortOrder)`), e fai usare l'harness anche a questo test. Se preferisci non rifattorizzare ora, inlinea il setup nel test copiandolo da `HomeMultiSelectionTests`. In entrambi i casi il fake tree-query deve restituire righe con `SortOrder` valorizzato.

- [ ] **Step 4: Run the test to verify it fails**

Run: `dotnet test tests/Recordum.WPF.Tests/Recordum.WPF.Tests.csproj --filter "FullyQualifiedName~Manual_sort_orders_roots"`
Expected: FAIL — l'ordine è `second, first` (Manual oggi è `_ => rows`).

- [ ] **Step 5: Make `HomeSort.Manual` order by SortOrder**

In `HomeViewModel.cs`, `ApplySort` (riga 438-443):

```csharp
    private IEnumerable<HomeTreeRow> ApplySort(IReadOnlyList<HomeTreeRow> rows) => SortOption switch
    {
        HomeSort.Name => rows.OrderBy(r => r.Name, StringComparer.CurrentCultureIgnoreCase),
        HomeSort.LastCompletion => rows.OrderByDescending(r => r.LastCompletionAt ?? DateTime.MinValue),
        HomeSort.Manual => rows.OrderBy(r => r.SortOrder).ThenBy(r => r.Name, StringComparer.CurrentCultureIgnoreCase),
        _ => rows,
    };
```

- [ ] **Step 6: Run the test to verify it passes + fix other HomeTreeRow constructions**

Run: `dotnet build src/Recordum.WPF/Recordum.WPF.csproj`
Expected: potrebbero comparire errori di compilazione dove `HomeTreeRow` è costruito senza `SortOrder` (fake di test, seed). Cerca le occorrenze:

Run: `dotnet test tests/Recordum.WPF.Tests/Recordum.WPF.Tests.csproj --filter "FullyQualifiedName~Manual_sort_orders_roots"`
Expected: PASS dopo aver aggiunto `SortOrder: 0` (o il valore di test) a ogni costruzione di `HomeTreeRow` che il compilatore segnala (tipicamente nei fixture di test WPF). Aggiorna ogni call-site rotto.

- [ ] **Step 7: Commit**

```bash
git add src/Recordum.Core/Abstractions/Services/Dtos/HomeTreeRow.cs src/Recordum.Data/Services/Queries/HabitTreeQueryService.cs src/Recordum.WPF/ViewModels/HomeViewModel.cs tests/Recordum.WPF.Tests/
git commit -m "feat(wpf): activate HomeSort.Manual — order rows by HabitNode.SortOrder"
```

---

## Task 3: Flag riga (`IsBeingDragged`, `DropEdge`) + `ApplyDropAsync` con auto-switch

**Files:**
- Create: `src/Recordum.WPF/ViewModels/RowDropEdge.cs`
- Modify: `src/Recordum.WPF/ViewModels/HomeRowViewModel.cs:77`
- Modify: `src/Recordum.WPF/ViewModels/HomeViewModel.cs`
- Test: `tests/Recordum.WPF.Tests/ViewModels/HomeDropDispatchTests.cs` (nuovo)

- [ ] **Step 1: Create the enum**

`src/Recordum.WPF/ViewModels/RowDropEdge.cs`:

```csharp
namespace Recordum.WPF.ViewModels;

/// <summary>Which insertion edge to draw on a row while a drag hovers it.</summary>
public enum RowDropEdge
{
    None,
    Before,
    After,
}
```

- [ ] **Step 2: Add the row flags**

In `HomeRowViewModel.cs`, subito dopo `_isDropTarget` (riga 77):

```csharp
    /// <summary>True for the row currently being dragged (drives the lifted/ghosted look).</summary>
    [ObservableProperty] private bool _isBeingDragged;

    /// <summary>Insertion indicator edge while a drag hovers this row (reorder affordance).</summary>
    [ObservableProperty] private RowDropEdge _dropEdge;
```

- [ ] **Step 3: Write the failing test for `ApplyDropAsync`**

Crea `tests/Recordum.WPF.Tests/ViewModels/HomeDropDispatchTests.cs`. Riusa lo stesso harness/setup di Task 2 (o inlinea). Due test:

```csharp
using FluentAssertions;
using Recordum.WPF.Behaviors;
using Recordum.WPF.ViewModels;
using Xunit;

namespace Recordum.WPF.Tests.ViewModels;

public class HomeDropDispatchTests
{
    [Fact]
    public async Task Reorder_drop_switches_sort_to_manual_and_calls_reorder()
    {
        var harness = HomeTestHarness.BuildFull(/* two sibling habits a,b under no parent */);
        var sut = harness.ViewModel;
        await sut.LoadCommand.ExecuteAsync(null);
        sut.SortOption = HomeSort.Name; // start in a non-manual sort

        var src = sut.Rows.First(r => r.Name == "a");
        var target = sut.Rows.First(r => r.Name == "b");
        var plan = new DropPlan(Parent: target.ParentId, Anchor: target.HabitNodeId, PlaceAfter: true);

        await sut.ApplyDropAsync(src, plan);

        sut.SortOption.Should().Be(HomeSort.Manual);
        harness.Nodes.ReorderCalls.Should().ContainSingle()
            .Which.Should().Be((src.HabitNodeId, target.ParentId, target.HabitNodeId, true));
    }

    [Fact]
    public async Task Onto_group_drop_appends_without_switching_sort()
    {
        var harness = HomeTestHarness.BuildFull(/* habit a + group g */);
        var sut = harness.ViewModel;
        await sut.LoadCommand.ExecuteAsync(null);
        sut.SortOption = HomeSort.Name;

        var src = sut.Rows.First(r => r.Name == "a");
        var group = sut.Rows.First(r => r.Name == "g");
        var plan = new DropPlan(Parent: group.HabitNodeId, Anchor: null, PlaceAfter: false);

        await sut.ApplyDropAsync(src, plan);

        sut.SortOption.Should().Be(HomeSort.Name); // append does not force Manual
        harness.Nodes.ReorderCalls.Should().ContainSingle()
            .Which.Should().Be((src.HabitNodeId, group.HabitNodeId, (Guid?)null, false));
    }
}
```

> `DropPlan` viene definito in Task 4 (`Recordum.WPF.Behaviors`). Se esegui Task 3 prima di Task 4, definisci `DropPlan` ora (Task 4 Step 1) — è un piccolo record condiviso.

- [ ] **Step 4: Run the test to verify it fails**

Run: `dotnet test tests/Recordum.WPF.Tests/Recordum.WPF.Tests.csproj --filter "FullyQualifiedName~HomeDropDispatchTests"`
Expected: FAIL — `ApplyDropAsync` / `DropPlan` non esistono.

- [ ] **Step 5: Implement `ApplyDropAsync` in `HomeViewModel`**

In `HomeViewModel.cs`, aggiungi (vicino a `TryBulkMoveAsync`, riga 198):

```csharp
    /// <summary>
    /// Applies a resolved drop: bulk selection → reparent in blocco (existing semantics);
    /// single row → MoveAndReorderAsync. A fine-grained reorder (anchor != null) forces
    /// Manual sort so the new order is visible and sticks.
    /// </summary>
    public async Task ApplyDropAsync(HomeRowViewModel src, DropPlan plan)
    {
        if (await TryBulkMoveAsync(src, plan.Parent))
            return; // bulk path reparents and reloads by itself

        try
        {
            await _nodes.MoveAndReorderAsync(src.HabitNodeId, plan.Parent, plan.Anchor, plan.PlaceAfter);
        }
        catch (CyclicHabitNodeMoveException)
        {
            return;
        }

        if (plan.Anchor is not null && SortOption != HomeSort.Manual)
            SortOption = HomeSort.Manual; // setter triggers Rebuild via OnSortOptionChanged
        else
            await LoadCommand.ExecuteAsync(null);
    }
```

> Verifica che `using Recordum.Core.Errors;` e `using Recordum.WPF.Behaviors;` siano presenti in cima a `HomeViewModel.cs`; aggiungili se mancano.

- [ ] **Step 6: Run the test to verify it passes**

Run: `dotnet test tests/Recordum.WPF.Tests/Recordum.WPF.Tests.csproj --filter "FullyQualifiedName~HomeDropDispatchTests"`
Expected: PASS (2 test).

- [ ] **Step 7: Commit**

```bash
git add src/Recordum.WPF/ViewModels/RowDropEdge.cs src/Recordum.WPF/ViewModels/HomeRowViewModel.cs src/Recordum.WPF/ViewModels/HomeViewModel.cs tests/Recordum.WPF.Tests/ViewModels/HomeDropDispatchTests.cs
git commit -m "feat(wpf): row drag flags + HomeViewModel.ApplyDropAsync with auto-switch to Manual"
```

---

## Task 4: `TreeDragDropBehavior` — `ResolveDrop` puro + indicatore singolo + dispatch

**Files:**
- Modify: `src/Recordum.WPF/Behaviors/TreeDragDropBehavior.cs`
- Modify: `tests/Recordum.WPF.Tests/Behaviors/TreeDragDropBehaviorTests.cs`

- [ ] **Step 1: Write the failing test for `ResolveDrop`**

In `TreeDragDropBehaviorTests.cs`, aggiungi:

```csharp
    [Fact]
    public void Onto_group_plans_append_at_end()
    {
        var target = Guid.NewGuid();
        var plan = TreeDragDropBehavior.ResolveDrop(
            TreeDragDropBehavior.DropZone.Onto, targetIsGroup: true, targetId: target, targetParentId: null);
        plan.Should().Be(new TreeDragDropBehavior.DropPlan(target, null, false));
    }

    [Fact]
    public void Before_plans_insert_before_target_among_its_siblings()
    {
        var parent = Guid.NewGuid();
        var target = Guid.NewGuid();
        var plan = TreeDragDropBehavior.ResolveDrop(
            TreeDragDropBehavior.DropZone.Before, targetIsGroup: false, targetId: target, targetParentId: parent);
        plan.Should().Be(new TreeDragDropBehavior.DropPlan(parent, target, false));
    }

    [Fact]
    public void After_plans_insert_after_target_among_its_siblings()
    {
        var parent = Guid.NewGuid();
        var target = Guid.NewGuid();
        var plan = TreeDragDropBehavior.ResolveDrop(
            TreeDragDropBehavior.DropZone.After, targetIsGroup: false, targetId: target, targetParentId: parent);
        plan.Should().Be(new TreeDragDropBehavior.DropPlan(parent, target, true));
    }

    [Fact]
    public void Onto_non_group_falls_back_to_sibling_after_target()
    {
        var parent = Guid.NewGuid();
        var target = Guid.NewGuid();
        var plan = TreeDragDropBehavior.ResolveDrop(
            TreeDragDropBehavior.DropZone.Onto, targetIsGroup: false, targetId: target, targetParentId: parent);
        plan.Should().Be(new TreeDragDropBehavior.DropPlan(parent, target, true));
    }
```

Aggiungi `using FluentAssertions;` se non già presente (lo è).

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/Recordum.WPF.Tests/Recordum.WPF.Tests.csproj --filter "FullyQualifiedName~plans"`
Expected: FAIL — `ResolveDrop` / `DropPlan` non esistono.

- [ ] **Step 3: Add `DropPlan` + `ResolveDrop`**

In `TreeDragDropBehavior.cs`, dentro la classe dopo l'enum `DropZone` (riga 16):

```csharp
    /// <summary>Resolved drop intent: new parent, the sibling to anchor next to (null = append), and side.</summary>
    public readonly record struct DropPlan(Guid? Parent, Guid? Anchor, bool PlaceAfter);

    /// <summary>
    /// Pure mapping from a hovered zone+target to a drop plan.
    /// Onto a group → append inside it. Onto a habit → sibling after it. Before/After → reorder around the target.
    /// </summary>
    public static DropPlan ResolveDrop(DropZone zone, bool targetIsGroup, Guid targetId, Guid? targetParentId) => zone switch
    {
        DropZone.Onto when targetIsGroup => new DropPlan(targetId, null, false),
        DropZone.Before => new DropPlan(targetParentId, targetId, false),
        // After, and Onto-on-a-habit, both place the row right after the target among its siblings.
        _ => new DropPlan(targetParentId, targetId, true),
    };
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test tests/Recordum.WPF.Tests/Recordum.WPF.Tests.csproj --filter "FullyQualifiedName~plans"`
Expected: PASS (4 test).

- [ ] **Step 5: Rewire `OnDragOver` to a single indicator**

Sostituisci `OnDragOver` (riga 79-92), `OnDragLeave` (riga 94) e `SetDropTarget` (riga 96-102) con un indicatore unico che imposta `IsDropTarget` **oppure** `DropEdge`:

```csharp
    // Drop affordance: a single indicator at a time — either the group "into" highlight
    // (IsDropTarget) or an insertion edge (DropEdge) on the hovered sibling.
    private static void OnDragOver(object sender, DragEventArgs e)
    {
        _dragAdorner?.SetPosition(e.GetPosition(sender as IInputElement));

        if (e.Data.GetData(typeof(HomeRowViewModel)) is HomeRowViewModel src
            && e.OriginalSource is FrameworkElement fe
            && fe.DataContext is HomeRowViewModel row
            && !ReferenceEquals(row, src))
        {
            var zone = ResolveZone(e.GetPosition(fe).Y / Math.Max(1, fe.ActualHeight));
            if (zone == DropZone.Onto && row.IsGroup)
                SetIndicator(row, isGroupTarget: true, RowDropEdge.None);
            else
                SetIndicator(row, isGroupTarget: false, zone == DropZone.Before ? RowDropEdge.Before : RowDropEdge.After);
        }
        else
        {
            SetIndicator(null, false, RowDropEdge.None);
        }
    }

    private static void OnDragLeave(object sender, DragEventArgs e) => SetIndicator(null, false, RowDropEdge.None);

    private static void SetIndicator(HomeRowViewModel? row, bool isGroupTarget, RowDropEdge edge)
    {
        if (_indicated is not null)
        {
            _indicated.IsDropTarget = false;
            _indicated.DropEdge = RowDropEdge.None;
        }
        _indicated = row;
        if (_indicated is not null)
        {
            _indicated.IsDropTarget = isGroupTarget;
            _indicated.DropEdge = edge;
        }
    }
```

Sostituisci il campo `private static HomeRowViewModel? _dropTarget;` (riga 29) con:

```csharp
    private static HomeRowViewModel? _indicated;
    private static DragAdorner? _dragAdorner;
```

> `DragAdorner` arriva in Task 5; per ora dichiara il campo `_dragAdorner` (resterà null finché Task 5 non lo popola — la chiamata `?.SetPosition` è null-safe). Se vuoi tenere i task compilabili in isolamento, fai Task 5 subito dopo questo step.

- [ ] **Step 6: Rewire `OnDrop` to use the plan + dispatch**

Sostituisci `OnDrop` (riga 104-133) con:

```csharp
    private static async void OnDrop(object sender, DragEventArgs e)
    {
        SetIndicator(null, false, RowDropEdge.None);
        if (sender is not ItemsControl ic) return;
        if (e.Data.GetData(typeof(HomeRowViewModel)) is not HomeRowViewModel src) return;
        if (e.OriginalSource is not FrameworkElement fe || fe.DataContext is not HomeRowViewModel target) return;
        if (ReferenceEquals(src, target)) return;
        if (ic.DataContext is not HomeViewModel home) return;

        var zone = ResolveZone(e.GetPosition(fe).Y / Math.Max(1, fe.ActualHeight));
        var plan = ResolveDrop(zone, target.IsGroup, target.HabitNodeId, target.ParentId);
        await home.ApplyDropAsync(src, plan);
    }
```

- [ ] **Step 7: Run the full WPF suite to verify nothing regressed**

Run: `dotnet test tests/Recordum.WPF.Tests/Recordum.WPF.Tests.csproj`
Expected: PASS (il noto flaky `CompletionRecorded_event_reloads_the_list_for_visual_feedback` può fallire in suite completa; verde isolato).

- [ ] **Step 8: Commit**

```bash
git add src/Recordum.WPF/Behaviors/TreeDragDropBehavior.cs tests/Recordum.WPF.Tests/Behaviors/TreeDragDropBehaviorTests.cs
git commit -m "feat(wpf): drag behavior — ResolveDrop plan, single indicator, dispatch to ApplyDropAsync"
```

---

## Task 5: `DragAdorner` — ghost che segue il cursore

Visuale, non unit-testata. Verifica = build + check manuale.

**Files:**
- Create: `src/Recordum.WPF/Behaviors/DragAdorner.cs`
- Modify: `src/Recordum.WPF/Behaviors/TreeDragDropBehavior.cs` (`OnMove`)

- [ ] **Step 1: Create the adorner**

`src/Recordum.WPF/Behaviors/DragAdorner.cs`:

```csharp
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace Recordum.WPF.Behaviors;

/// <summary>
/// A semi-transparent snapshot of the dragged row that follows the cursor.
/// Single purpose: render the ghost. No domain logic.
/// </summary>
public sealed class DragAdorner : Adorner
{
    private readonly Rect _bounds;
    private readonly Brush _ghost;
    private Point _offset;

    public DragAdorner(UIElement adornedElement, FrameworkElement source) : base(adornedElement)
    {
        IsHitTestVisible = false;
        _bounds = new Rect(new Size(source.ActualWidth, source.ActualHeight));
        _ghost = new VisualBrush(source) { Opacity = 0.65, Stretch = Stretch.None, AlignmentX = AlignmentX.Left, AlignmentY = AlignmentY.Top };
    }

    /// <summary>Position is the cursor point relative to the adorned element; the ghost is offset
    /// slightly down-right so the cursor stays visible.</summary>
    public void SetPosition(Point cursor)
    {
        _offset = new Point(cursor.X + 8, cursor.Y + 8);
        (Parent as AdornerLayer)?.Update(AdornedElement);
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        dc.PushOpacity(0.85);
        dc.DrawRectangle(_ghost, null, new Rect(_offset, _bounds.Size));
        dc.Pop();
    }
}
```

- [ ] **Step 2: Spawn/teardown in `OnMove`**

In `TreeDragDropBehavior.cs`, sostituisci `OnMove` (riga 65-75) con:

```csharp
    private static void OnMove(object sender, MouseEventArgs e)
    {
        if (!_armed || e.LeftButton != MouseButtonState.Pressed) return;
        var p = e.GetPosition(sender as IInputElement);
        if ((p - _start).Length < 5) return;
        if (sender is ItemsControl ic
            && e.OriginalSource is FrameworkElement fe
            && fe.DataContext is HomeRowViewModel src)
        {
            _armed = false;
            var layer = AdornerLayer.GetAdornerLayer(ic);
            _dragAdorner = new DragAdorner(ic, fe);
            layer?.Add(_dragAdorner);
            src.IsBeingDragged = true;
            try
            {
                DragDrop.DoDragDrop(fe, src, DragDropEffects.Move);
            }
            finally
            {
                // DoDragDrop is blocking; this runs on drop OR cancel, wherever it ends.
                if (layer is not null && _dragAdorner is not null) layer.Remove(_dragAdorner);
                _dragAdorner = null;
                src.IsBeingDragged = false;
                SetIndicator(null, false, RowDropEdge.None);
            }
        }
    }
```

Aggiungi gli `using` necessari in cima al file: `using System.Windows.Documents;` (per `AdornerLayer`).

- [ ] **Step 3: Build to verify it compiles**

Run: `dotnet build src/Recordum.WPF/Recordum.WPF.csproj`
Expected: Build succeeded, 0 errori.

- [ ] **Step 4: Run the WPF suite**

Run: `dotnet test tests/Recordum.WPF.Tests/Recordum.WPF.Tests.csproj`
Expected: PASS (flaky noto a parte).

- [ ] **Step 5: Commit**

```bash
git add src/Recordum.WPF/Behaviors/DragAdorner.cs src/Recordum.WPF/Behaviors/TreeDragDropBehavior.cs
git commit -m "feat(wpf): DragAdorner — cursor-following ghost during row drag"
```

---

## Task 6: XAML — sbiadimento originale + linea di inserimento

**Files:**
- Modify: `src/Recordum.WPF/App.xaml:73-86` (triggers nello stile `Recordum.HabitBlock`)

- [ ] **Step 1: Add the triggers**

In `App.xaml`, dentro `<Style.Triggers>` dello stile `Recordum.HabitBlock` (dopo il `DataTrigger` `IsDropTarget`, riga 85), aggiungi:

```xml
                    <!-- Source row while it is being dragged: ghosted in place. -->
                    <DataTrigger Binding="{Binding IsBeingDragged}" Value="True">
                        <Setter Property="Opacity" Value="0.4"/>
                    </DataTrigger>
                    <!-- Insertion indicators for manual reorder (thin accent edge). -->
                    <DataTrigger Binding="{Binding DropEdge}" Value="Before">
                        <Setter Property="BorderBrush" Value="{DynamicResource Brush.Accent}"/>
                        <Setter Property="BorderThickness" Value="0,2,0,0"/>
                    </DataTrigger>
                    <DataTrigger Binding="{Binding DropEdge}" Value="After">
                        <Setter Property="BorderBrush" Value="{DynamicResource Brush.Accent}"/>
                        <Setter Property="BorderThickness" Value="0,0,0,2"/>
                    </DataTrigger>
```

> WPF converte la stringa `"Before"`/`"After"` nel valore enum `RowDropEdge` automaticamente nel `DataTrigger`. L'ordine dopo `IsDropTarget` garantisce che gli indicatori di riordino vincano quando applicabili.

- [ ] **Step 2: Build to verify XAML compiles**

Run: `dotnet build src/Recordum.WPF/Recordum.WPF.csproj`
Expected: Build succeeded, 0 errori.

- [ ] **Step 3: Commit**

```bash
git add src/Recordum.WPF/App.xaml
git commit -m "feat(wpf): habit-row drag visuals — ghosted source + insertion edge"
```

---

## Task 7: Verifica finale + verifica visiva

- [ ] **Step 1: Full build + all suites**

Run: `dotnet build` poi `dotnet test`
Expected: tutte verdi (flaky noto `CompletionRecorded_event_reloads_the_list_for_visual_feedback` a parte — riesegui isolato se rosso).

- [ ] **Step 2: Verifica visiva manuale (skill `run`)**

Lancia l'app (eventualmente `--seed-demo` per avere gruppi+abitudini) e verifica:
- Tenendo premuto e trascinando una riga, un **fantasma semitrasparente segue il cursore** e l'originale si **sbiadisce** sul posto.
- Sopra/sotto un'abitudine compare una **linea accent** (riordino); sopra un gruppo compare l'**evidenziazione** del gruppo (entra-dentro). Mai due indicatori insieme.
- Rilasciando sopra/sotto un'abitudine la lista **riordina** e, se eri in Nome/Ultimo completamento, passa a **Manuale**; l'ordine **persiste al riavvio**.
- Rilasciando su un gruppo l'abitudine **entra** nel gruppo.
- Funziona in tema **chiaro e scuro**.

- [ ] **Step 3: Update tracker**

Aggiorna `docs/phase-4-issues.md` con una voce per questo lavoro (stato `fixed`, verifica visiva utente da fare) e committa.

---

## Self-Review (autore del piano)

**Spec coverage:**
- §4.1 SortOrder in HomeTreeRow + Manual attivo → Task 2 ✓; servizio riordino (anchor-based) → Task 1 ✓; auto-switch a Manual → Task 3 (`ApplyDropAsync`) ✓.
- §4.2 zone, indicatore singolo, spawn/teardown adorner, dispatch, multi-selezione → Task 4 + Task 5 ✓.
- §4.3 `IsBeingDragged`/`DropEdge`/`DragAdorner` + trigger XAML → Task 3, 5, 6 ✓.
- §5 test: servizio riordino (Task 1), Manual ordina (Task 2), auto-switch (Task 3), ResolveDrop (Task 4) ✓.
- §6 limiti noti (bulk reparent-only) → preservato in `ApplyDropAsync` via `TryBulkMoveAsync` ✓.

**Type consistency:** `MoveAndReorderAsync(Guid, Guid?, Guid?, bool, CancellationToken)` identico in interfaccia/impl/fake/test. `DropPlan(Guid? Parent, Guid? Anchor, bool PlaceAfter)` usato uniforme in Behavior, HomeViewModel, test. `RowDropEdge {None,Before,After}` coerente VM/XAML. `SetIndicator(HomeRowViewModel?, bool, RowDropEdge)` firma unica.

**Placeholder scan:** nessun TBD/TODO; ogni step di codice ha il codice completo. L'unico punto a discrezione dell'esecutore è il riuso vs inline del setup Home nei test (Task 2 Step 3 / Task 3 Step 3) — istruzione esplicita data, non un placeholder di implementazione.
