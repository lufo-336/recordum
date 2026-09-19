using FluentAssertions;
using Recordum.WPF.Tests.Fixtures;
using Recordum.Presentation.ViewModels.HabitScreen.Notes;
using Xunit;

namespace Recordum.WPF.Tests.ViewModels.HabitScreen.Notes;

public class AttachmentViewModelTests
{
    [Fact]
    public void Maps_fields_and_resolves_absolute_path_via_service()
    {
        var attachments = new FakeAttachmentService();
        var attachment = new Recordum.Core.Models.Attachment
        {
            Id = Guid.NewGuid(),
            NoteId = Guid.NewGuid(),
            Filename = "photo.jpg",
            MimeType = "image/jpeg",
            FileSizeBytes = 4096,
        };

        var sut = new AttachmentViewModel(attachment, attachments);

        sut.Id.Should().Be(attachment.Id);
        sut.NoteId.Should().Be(attachment.NoteId);
        sut.Filename.Should().Be("photo.jpg");
        sut.MimeType.Should().Be("image/jpeg");
        sut.FileSizeBytes.Should().Be(4096);
        sut.FilePath.Should().Be(attachments.ResolveAbsolutePath(attachment));
    }
}
