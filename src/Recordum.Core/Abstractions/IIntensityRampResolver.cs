using Recordum.Core.Models;

namespace Recordum.Core.Abstractions;

public interface IIntensityRampResolver
{
    IntensityRamp Resolve(HabitNode node, HabitConfig? config, Goal? activeGoal);
}

public sealed record IntensityRamp(decimal Step1, decimal Step2, decimal Step3, decimal Step4)
{
    public static IntensityRamp Fallback { get; } = new(1m, 3m, 7m, 8m);
}
