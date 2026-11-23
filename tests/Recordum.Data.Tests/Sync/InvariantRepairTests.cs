using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Data.Services;
using Recordum.Data.Tests.Fixtures.Sync;

namespace Recordum.Data.Tests.Sync;

/// <summary>Spec §8.3: riparazione delle invarianti di dominio dopo l'unione (due periodi d'astinenza aperti sulla stessa
/// abitudine; ciclo in <c>habit_nodes.parent_id</c>).</summary>
public sealed class InvariantRepairTests
{
    private static readonly Guid H = new("44444444-0000-0000-0000-000000000001");
    private static readonly Guid P1 = new("44444444-0000-0000-0000-000000000002");
    private static readonly Guid P2 = new("44444444-0000-0000-0000-000000000003");
    private static readonly Guid G0 = new("44444444-0000-0000-0000-000000000010");
    private static readonly Guid G1 = new("44444444-0000-0000-0000-000000000011");
    private static readonly Guid G2 = new("44444444-0000-0000-0000-000000000012");

    private static HabitNode Group(Guid id, string name, Guid? parentId = null) => new()
    {
        Id = id,
        ParentId = parentId,
        Name = name,
        NodeType = NodeType.Group,
        HabitType = null,
        Color = "#808080",
    };

    [Fact]
    public async Task TwoOpenAbstinencePeriodsRepaired()
    {
        await using var a = new SyncDevice("a");
        await using var b = new SyncDevice("b");

        const long start1Ms = 1_767_225_600_000; // 2026-01-01 00:00:00 UTC
        const long start2Ms = 1_767_312_000_000; // 2026-01-02 00:00:00 UTC

        await a.EditAsync(100, db => db.HabitNodes.Add(new HabitNode
        {
            Id = H,
            Name = "No sugar",
            NodeType = NodeType.Habit,
            HabitType = HabitType.Resistance,
            Color = "#808080",
        }));
        await b.MergeAsync(await a.WriteFileAsync());

        // A apre il periodo P1 prima; B (offline) apre il periodo P2 dopo.
        await a.EditAsync(start1Ms, db => db.AbstinencePeriods.Add(new AbstinencePeriod
        {
            Id = P1,
            HabitNodeId = H,
            StartedAtMs = start1Ms,
            EndedAtMs = null,
        }));

        await b.EditAsync(start2Ms, db => db.AbstinencePeriods.Add(new AbstinencePeriod
        {
            Id = P2,
            HabitNodeId = H,
            StartedAtMs = start2Ms,
            EndedAtMs = null,
        }));

        var serviceBeforeOnA = new AbstinenceService(a.Fixture.Factory, a.Clock, null!, a.Fixture.ResetTimes);
        var stateBefore = await serviceBeforeOnA.GetStateAsync(H);
        var periodsBefore = await serviceBeforeOnA.ListPeriodsAsync(H, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 5));
        var totalDurationBefore = periodsBefore
            .Where(p => p.EndedAtMs is not null)
            .Sum(p => p.EndedAtMs!.Value - p.StartedAtMs);

        var reportB = await b.MergeAsync(await a.WriteFileAsync());
        reportB.Repaired.Should().Be(1);

        var reportA = await a.MergeAsync(await b.WriteFileAsync());
        reportA.Repaired.Should().Be(0, "B already repaired P2 and sent the closed period to A");

        foreach (var d in new[] { a, b })
        {
            var p1 = await d.QueryAsync(db => db.AbstinencePeriods.AsNoTracking().SingleAsync(p => p.Id == P1));
            var p2 = await d.QueryAsync(db => db.AbstinencePeriods.AsNoTracking().SingleAsync(p => p.Id == P2));

            p1.EndedAtMs.Should().BeNull("the period that started earlier stays open");
            p2.EndedAtMs.Should().Be(start2Ms, "the later open period is closed at its own start (duration zero)");

            var service = new AbstinenceService(d.Fixture.Factory, d.Clock, null!, d.Fixture.ResetTimes);
            var stateAfter = await service.GetStateAsync(H);
            stateAfter.Should().Be(new AbstinenceState(AbstinenceStatus.Abstaining, start1Ms, null));
            stateAfter.Should().Be(stateBefore, "a zero-duration repaired period does not alter the ongoing streak");

            var periodsAfter = await service.ListPeriodsAsync(H, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 5));
            var totalClosedDurationAfter = periodsAfter
                .Where(p => p.EndedAtMs is not null)
                .Sum(p => p.EndedAtMs!.Value - p.StartedAtMs);
            totalClosedDurationAfter.Should().Be(totalDurationBefore, "a zero-duration period contributes 0 ms to closed durations");
        }

        (await a.CanonicalDumpAsync()).Should().Be(await b.CanonicalDumpAsync());
    }

    [Fact]
    public async Task ParentCycleBroken()
    {
        await using var a = new SyncDevice("a");
        await using var b = new SyncDevice("b");

        // Stato iniziale condiviso: G1 sotto G0, G2 in radice.
        await a.EditAsync(100, db => db.HabitNodes.AddRange(
            Group(G0, "Root Group"),
            Group(G1, "Group 1", parentId: G0),
            Group(G2, "Group 2", parentId: null)));
        await b.MergeAsync(await a.WriteFileAsync());
        await a.MergeAsync(await b.WriteFileAsync());

        // A sposta G2 sotto G1 a t=200; B sposta G1 sotto G2 a t=300 → ciclo G1 <-> G2.
        await a.EditAsync(200, async db => (await db.HabitNodes.SingleAsync(n => n.Id == G2)).ParentId = G1);
        await b.EditAsync(300, async db => (await db.HabitNodes.SingleAsync(n => n.Id == G1)).ParentId = G2);

        var fa = await a.WriteFileAsync();
        var fb = await b.WriteFileAsync();
        var reportB = await b.MergeAsync(fa);
        var reportA = await a.MergeAsync(fb);

        reportB.Repaired.Should().Be(1);
        reportA.Repaired.Should().Be(1);

        // Un secondo giro allinea l'ora della riparazione.
        await b.MergeAsync(await a.WriteFileAsync());
        await a.MergeAsync(await b.WriteFileAsync());

        foreach (var d in new[] { a, b })
        {
            var g1 = await d.QueryAsync(db => db.HabitNodes.AsNoTracking().SingleAsync(n => n.Id == G1));
            var g2 = await d.QueryAsync(db => db.HabitNodes.AsNoTracking().SingleAsync(n => n.Id == G2));

            g2.ParentId.Should().Be(G1, "the earlier reparenting at t=200 wins");
            g1.ParentId.Should().Be(G0, "the newer reparenting at t=300 loses and reverts to its previous parent from sync_base");
        }

        (await a.CanonicalDumpAsync()).Should().Be(await b.CanonicalDumpAsync());
    }

    [Fact]
    public async Task CycleRepairKeepsAConcurrentRenameElsewhere()
    {
        await using var a = new SyncDevice("a");
        await using var b = new SyncDevice("b");

        // Stato iniziale condiviso: X (G1) sotto G0, Y (G2) in radice.
        await a.EditAsync(100, db => db.HabitNodes.AddRange(
            Group(G0, "Root Group"),
            Group(G1, "X", parentId: G0),
            Group(G2, "Y", parentId: null)));
        await b.MergeAsync(await a.WriteFileAsync());
        await a.MergeAsync(await b.WriteFileAsync());

        // B sposta Y sotto X a t=200; A sposta X sotto Y a t=300; poi B rinomina X a t=400.
        // Perde lo spostamento più recente (X a t=300): la riparazione tocca X.parent_id senza alzare X.name né gli altri campi.
        await b.EditAsync(200, async db => (await db.HabitNodes.SingleAsync(n => n.Id == G2)).ParentId = G1);
        await a.EditAsync(300, async db => (await db.HabitNodes.SingleAsync(n => n.Id == G1)).ParentId = G2);
        await b.EditAsync(400, async db => (await db.HabitNodes.SingleAsync(n => n.Id == G1)).Name = "X renamed");

        var fa = await a.WriteFileAsync();
        var fb = await b.WriteFileAsync();
        var reportB = await b.MergeAsync(fa);
        var reportA = await a.MergeAsync(fb);

        reportB.Repaired.Should().Be(1);
        reportA.Repaired.Should().Be(1);

        await b.MergeAsync(await a.WriteFileAsync());
        await a.MergeAsync(await b.WriteFileAsync());

        var xId = G1.ToString("D").ToUpperInvariant();
        foreach (var d in new[] { a, b })
        {
            var x = await d.QueryAsync(db => db.HabitNodes.AsNoTracking().SingleAsync(n => n.Id == G1));
            var y = await d.QueryAsync(db => db.HabitNodes.AsNoTracking().SingleAsync(n => n.Id == G2));

            y.ParentId.Should().Be(G1, "Y moved at t=200 before X moved at t=300");
            x.ParentId.Should().Be(G0, "X's move at t=300 lost and reverted to G0");
            x.Name.Should().Be("X renamed", "concurrent rename on X survives cycle repair");

            var fieldMs = await d.QueryAsync(db => db.SyncFieldMs.AsNoTracking()
                .Where(f => f.Entity == "habit_node" && f.EntityId == xId)
                .ToDictionaryAsync(f => f.Field, f => f.Ms));
            fieldMs.Should().ContainKey("name").WhoseValue.Should().Be(400);
            fieldMs.Should().ContainKey("color").WhoseValue.Should().Be(100);
            fieldMs.Values.Should().OnlyContain(ms => ms < x.UpdatedAtMs);
        }

        (await a.CanonicalDumpAsync()).Should().Be(await b.CanonicalDumpAsync());
    }

    private static async Task MeshAsync(params SyncDevice[] ds)
    {
        var files = new List<(SyncDevice, byte[])>();
        foreach (var d in ds) files.Add((d, await d.WriteFileAsync()));
        foreach (var d in ds)
            foreach (var (src, f) in files)
                if (src != d) await d.MergeAsync(f);
    }

    [Fact]
    public async Task CycleRepairConvergesWithThreeDevices()
    {
        await using var a = new SyncDevice("a");
        await using var b = new SyncDevice("b");
        await using var c = new SyncDevice("c");

        await a.EditAsync(100, db => db.HabitNodes.AddRange(
            Group(G0, "Root Group", parentId: null),
            Group(G1, "Group 1", parentId: null),
            Group(G2, "Group 2", parentId: null)));
        var f0 = await a.WriteFileAsync();
        await b.MergeAsync(f0);
        await c.MergeAsync(f0);
        await a.MergeAsync(await b.WriteFileAsync());
        await a.MergeAsync(await c.WriteFileAsync());

        // A sposta G1 sotto G0 a t=150 e B unisce A; C resta con G1 in radice.
        await a.EditAsync(150, async db => (await db.HabitNodes.SingleAsync(n => n.Id == G1)).ParentId = G0);
        await b.MergeAsync(await a.WriteFileAsync());

        // A sposta G2 sotto G1 (t=200), B sposta G1 sotto G2 (t=300) -> ciclo G1 <-> G2.
        await a.EditAsync(200, async db => (await db.HabitNodes.SingleAsync(n => n.Id == G2)).ParentId = G1);
        await b.EditAsync(300, async db => (await db.HabitNodes.SingleAsync(n => n.Id == G1)).ParentId = G2);

        var fa = await a.WriteFileAsync();
        var fb = await b.WriteFileAsync();
        await a.MergeAsync(fb);
        await b.MergeAsync(fa);

        for (var i = 0; i < 3; i++)
            await MeshAsync(a, b, c);

        var dump = await a.CanonicalDumpAsync();
        (await b.CanonicalDumpAsync()).Should().Be(dump);
        (await c.CanonicalDumpAsync()).Should().Be(dump);

        await MeshAsync(a, b, c);
        (await a.CanonicalDumpAsync()).Should().Be(dump, "fixed point");

        Guid? expectedG1Parent = null;
        foreach (var d in new[] { a, b, c })
        {
            var g0 = await d.QueryAsync(db => db.HabitNodes.AsNoTracking().SingleAsync(n => n.Id == G0));
            var g1 = await d.QueryAsync(db => db.HabitNodes.AsNoTracking().SingleAsync(n => n.Id == G1));
            var g2 = await d.QueryAsync(db => db.HabitNodes.AsNoTracking().SingleAsync(n => n.Id == G2));

            g0.ParentId.Should().BeNull();
            g2.ParentId.Should().Be(G1);
            g1.ParentId.Should().NotBe(G1).And.NotBe(G2, "no cycle");
            expectedG1Parent ??= g1.ParentId;
            g1.ParentId.Should().Be(expectedG1Parent);
        }
    }
}
