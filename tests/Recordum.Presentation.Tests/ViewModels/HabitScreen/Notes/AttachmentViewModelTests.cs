using FluentAssertions;
using Recordum.Presentation.ViewModels.HabitScreen.Notes;
using Xunit;

namespace Recordum.Presentation.Tests.ViewModels.HabitScreen.Notes;

public class AttachmentViewModelTests
{
    [Fact]
    public void Maps_fields_and_carries_the_thumbnail_bytes()
    {
        var attachment = new Recordum.Core.Models.Attachment
        {
            Id = Guid.NewGuid(),
            NoteId = Guid.NewGuid(),
            Filename = "photo.jpg",
            MimeType = "image/jpeg",
            FileSizeBytes = 4096,
            IsMissing = true,
        };
        byte[] thumb = [1, 2, 3];

        var sut = new AttachmentViewModel(attachment, thumb);

        sut.Id.Should().Be(attachment.Id);
        sut.NoteId.Should().Be(attachment.NoteId);
        sut.Filename.Should().Be("photo.jpg");
        sut.MimeType.Should().Be("image/jpeg");
        sut.FileSizeBytes.Should().Be(4096);
        sut.Thumbnail.Should().BeSameAs(thumb);
        sut.IsMissing.Should().BeTrue();
    }
}
