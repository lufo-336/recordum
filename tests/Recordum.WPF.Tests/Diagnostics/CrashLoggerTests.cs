using System.IO;
using FluentAssertions;
using Recordum.WPF.Diagnostics;
using Xunit;

namespace Recordum.WPF.Tests.Diagnostics;

public sealed class CrashLoggerTests
{
    [Fact]
    public void WireUp_creates_log_file_in_localappdata()
    {
        CrashLogger.ResetForTests();

        CrashLogger.WireUp();

        CrashLogger.LogPath.Should().NotBeNull();
        CrashLogger.LogPath.Should().EndWith(Path.Combine("Recordum", "logs", "recordum.log"));
        File.Exists(CrashLogger.LogPath!).Should().BeTrue();
    }

    [Fact]
    public void Log_writes_exception_type_message_and_stacktrace()
    {
        CrashLogger.ResetForTests();
        CrashLogger.WireUp();
        var path = CrashLogger.LogPath!;
        var sizeBefore = new FileInfo(path).Length;

        Exception captured;
        try { throw new InvalidOperationException("smoke marker xyz"); }
        catch (Exception ex) { captured = ex; }

        CrashLogger.Log("CrashLoggerTests smoke", captured);

        var written = File.ReadAllText(path).Substring((int)sizeBefore);
        written.Should().Contain("CrashLoggerTests smoke");
        written.Should().Contain("System.InvalidOperationException");
        written.Should().Contain("smoke marker xyz");
        written.Should().Contain("at Recordum.WPF.Tests.Diagnostics.CrashLoggerTests"); // stack trace frame
    }

    [Fact]
    public void Log_unwinds_inner_and_aggregate_exceptions()
    {
        CrashLogger.ResetForTests();
        CrashLogger.WireUp();
        var path = CrashLogger.LogPath!;
        var sizeBefore = new FileInfo(path).Length;

        var aggregate = new AggregateException(
            "outer agg",
            new InvalidOperationException("inner one marker A"),
            new ArgumentException("inner two marker B"));

        CrashLogger.Log("agg smoke", aggregate);

        var written = File.ReadAllText(path).Substring((int)sizeBefore);
        written.Should().Contain("AggregateException");
        written.Should().Contain("inner one marker A");
        written.Should().Contain("inner two marker B");
        written.Should().Contain("--- aggregate inner ---");
    }

    [Fact]
    public void Log_is_safe_when_exception_is_null()
    {
        CrashLogger.ResetForTests();
        CrashLogger.WireUp();
        var path = CrashLogger.LogPath!;
        var sizeBefore = new FileInfo(path).Length;

        CrashLogger.Log("null exception source", ex: null);

        var written = File.ReadAllText(path).Substring((int)sizeBefore);
        written.Should().Contain("null exception source");
        written.Should().Contain("(no exception object)");
    }
}
