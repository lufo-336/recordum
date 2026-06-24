using System.Threading.Tasks;
using Recordum.WPF.Services;

namespace Recordum.WPF.Tests.Fixtures;

public sealed class FakeScreenColorPicker : IScreenColorPicker
{
    public string? Result { get; set; }
    public Task<string?> PickAsync() => Task.FromResult(Result);
}
