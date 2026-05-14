using Recordum.Core.Models;

namespace Recordum.Data.SeedData;

/// <summary>
/// Official seed data for UnitOfMeasure. GUIDs are stable constants required by EF Core HasData.
/// Timestamps are literal constants because HasData produces INSERT SQL — the TimestampInterceptor does not run here.
/// </summary>
public static class UnitOfMeasureSeed
{
    private static readonly DateTime SeedDate = new(2026, 5, 14, 0, 0, 0, DateTimeKind.Utc);

    public static IReadOnlyList<UnitOfMeasure> All { get; } =
    [
        new UnitOfMeasure
        {
            Id = Guid.Parse("00000001-0000-0000-0000-000000000001"),
            Code = "km",
            Name = "Kilometre",
            Symbol = "km",
            IsOfficial = true,
            CreatedAt = SeedDate,
        },
        new UnitOfMeasure
        {
            Id = Guid.Parse("00000001-0000-0000-0000-000000000002"),
            Code = "m",
            Name = "Metre",
            Symbol = "m",
            IsOfficial = true,
            CreatedAt = SeedDate,
        },
        new UnitOfMeasure
        {
            Id = Guid.Parse("00000001-0000-0000-0000-000000000003"),
            Code = "kg",
            Name = "Kilogram",
            Symbol = "kg",
            IsOfficial = true,
            CreatedAt = SeedDate,
        },
        new UnitOfMeasure
        {
            Id = Guid.Parse("00000001-0000-0000-0000-000000000004"),
            Code = "g",
            Name = "Gram",
            Symbol = "g",
            IsOfficial = true,
            CreatedAt = SeedDate,
        },
        new UnitOfMeasure
        {
            Id = Guid.Parse("00000001-0000-0000-0000-000000000005"),
            Code = "ml",
            Name = "Millilitre",
            Symbol = "ml",
            IsOfficial = true,
            CreatedAt = SeedDate,
        },
        new UnitOfMeasure
        {
            Id = Guid.Parse("00000001-0000-0000-0000-000000000006"),
            Code = "l",
            Name = "Litre",
            Symbol = "l",
            IsOfficial = true,
            CreatedAt = SeedDate,
        },
        new UnitOfMeasure
        {
            Id = Guid.Parse("00000001-0000-0000-0000-000000000007"),
            Code = "kcal",
            Name = "Kilocalorie",
            Symbol = "kcal",
            IsOfficial = true,
            CreatedAt = SeedDate,
        },
        new UnitOfMeasure
        {
            Id = Guid.Parse("00000001-0000-0000-0000-000000000008"),
            Code = "steps",
            Name = "Steps",
            Symbol = null,
            IsOfficial = true,
            CreatedAt = SeedDate,
        },
        new UnitOfMeasure
        {
            Id = Guid.Parse("00000001-0000-0000-0000-000000000009"),
            Code = "hours",
            Name = "Hours",
            Symbol = "h",
            IsOfficial = true,
            CreatedAt = SeedDate,
        },
        new UnitOfMeasure
        {
            Id = Guid.Parse("00000001-0000-0000-0000-000000000010"),
            Code = "minutes",
            Name = "Minutes",
            Symbol = "min",
            IsOfficial = true,
            CreatedAt = SeedDate,
        },
        new UnitOfMeasure
        {
            Id = Guid.Parse("00000001-0000-0000-0000-000000000011"),
            Code = "seconds",
            Name = "Seconds",
            Symbol = "s",
            IsOfficial = true,
            CreatedAt = SeedDate,
        },
        new UnitOfMeasure
        {
            Id = Guid.Parse("00000001-0000-0000-0000-000000000012"),
            Code = "pages",
            Name = "Pages",
            Symbol = null,
            IsOfficial = true,
            CreatedAt = SeedDate,
        },
        new UnitOfMeasure
        {
            Id = Guid.Parse("00000001-0000-0000-0000-000000000013"),
            Code = "reps",
            Name = "Repetitions",
            Symbol = null,
            IsOfficial = true,
            CreatedAt = SeedDate,
        },
        new UnitOfMeasure
        {
            Id = Guid.Parse("00000001-0000-0000-0000-000000000014"),
            Code = "sets",
            Name = "Sets",
            Symbol = null,
            IsOfficial = true,
            CreatedAt = SeedDate,
        },
    ];
}
