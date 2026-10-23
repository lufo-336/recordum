using Recordum.Plugins.Contracts.Export;
using Recordum.Presentation.Services;

namespace Recordum.Presentation.Tests.Services;

public sealed class ExportFolderTests
{
    private static readonly ExportOutput Out = new([9], [new ExportFile("images/x.jpg", [1])]);

    [Fact]
    public void Plan_puts_the_document_and_the_files_in_a_folder_named_like_the_file()
    {
        var plan = ExportFolder.Plan(Path.Combine("dir", "diary.md"), Out, _ => false);

        Assert.Equal(Path.Combine("dir", "diary", "diary.md"), plan[0].Path);
        Assert.Equal(Path.Combine("dir", "diary", "images", "x.jpg"), plan[1].Path);
    }

    [Fact]
    public void Plan_skips_existing_folders_with_a_numeric_suffix()
    {
        var taken = new HashSet<string> { Path.Combine("dir", "diary"), Path.Combine("dir", "diary (2)") };

        var plan = ExportFolder.Plan(Path.Combine("dir", "diary.md"), Out, taken.Contains);

        Assert.Equal(Path.Combine("dir", "diary (3)", "diary.md"), plan[0].Path);
    }

    [Theory]
    [InlineData("../x.jpg")]
    [InlineData("images/../../x.jpg")]
    [InlineData("/etc/x.jpg")]
    [InlineData("images\\..\\x.jpg")]
    public void Plan_rejects_unsafe_relative_paths(string relative)
    {
        var bad = new ExportOutput([9], [new ExportFile("images/ok.jpg", [1]), new ExportFile(relative, [1])]);

        Assert.Throws<InvalidDataException>(() => ExportFolder.Plan("diary.md", bad, _ => false));
    }
}
