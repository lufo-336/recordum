using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Data;

namespace Recordum.Data.Tests.Configurations;

/// <summary>
/// Verifies the Fluent API mapping for HabitNode.PinScope:
/// TEXT column, max length 8, default None, index, and check constraint.
///
/// Notes on EF Core 9 API surface:
/// - IReadOnlyProperty.ClrType always returns the declared C# type (PinScope enum),
///   NOT the converted/provider type. Use GetProviderClrType() to get the post-conversion type.
/// - GetCheckConstraints() requires the design-time model (IDesignTimeModel), not the
///   read-optimised runtime model exposed by ctx.Model. Use ctx.GetService&lt;IDesignTimeModel&gt;().
/// </summary>
public sealed class HabitNodeConfigurationTests
{
    private readonly IReadOnlyEntityType _habitNode;
    private readonly IModel _designTimeModel;

    public HabitNodeConfigurationTests()
    {
        var opts = new DbContextOptionsBuilder<RecordumDbContext>()
            .UseSqlite("DataSource=:memory:")
            .UseSnakeCaseNamingConvention()
            .Options;
        using var ctx = new RecordumDbContext(opts);

        // Runtime model — sufficient for property/index assertions.
        _habitNode = ctx.Model.FindEntityType(typeof(HabitNode))!;

        // Design-time model — required for relational metadata (check constraints).
        _designTimeModel = ctx.GetService<IDesignTimeModel>().Model;
    }

    [Fact]
    public void PinScope_is_stored_as_string_with_default_None()
    {
        var prop = _habitNode.FindProperty(nameof(HabitNode.PinScope))!;

        // Declared C# type is still the enum — the conversion is transparent to the domain.
        Assert.Equal(typeof(PinScope), prop.ClrType);

        // The provider (database) side must be string/TEXT.
        Assert.Equal(typeof(string), prop.GetProviderClrType());

        // MaxLength is 16 to match the convention used by other enum columns
        // (NodeType / HabitType / AiDataSharing). The CHECK constraint enforces the value set.
        Assert.Equal(16, prop.GetMaxLength());

        // Default value must be PinScope.None.
        Assert.Equal(PinScope.None, prop.GetDefaultValue());
    }

    [Fact]
    public void PinScope_has_index_and_check_constraint()
    {
        // Index is on the runtime entity type.
        Assert.Contains(
            _habitNode.GetIndexes(),
            i => i.Properties.Any(p => p.Name == nameof(HabitNode.PinScope)));

        // Check constraints live in the design-time model.
        var dtEntityType = _designTimeModel.FindEntityType(typeof(HabitNode))!;
        Assert.Contains(
            dtEntityType.GetCheckConstraints(),
            c => c.Name == "ck_habit_node_pin_scope");
    }
}
