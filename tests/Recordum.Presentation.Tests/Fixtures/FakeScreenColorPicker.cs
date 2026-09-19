using System.Threading.Tasks;
using Recordum.Presentation.Services;

namespace Recordum.Presentation.Tests.Fixtures;

public sealed class FakeScreenColorPicker : IScreenColorPicker
{
    public string? Result { get; set; }
    public Task<string?> PickAsync() => Task.FromResult(Result);
}
