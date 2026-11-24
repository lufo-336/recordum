using System.Globalization;
using FluentAssertions;
using Recordum.Presentation.Resources;
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

    [Fact]
    public void OriginalElsewhereText_explains_where_the_original_lives_when_content_is_absent()
    {
        var prevStrings = Strings.Culture;
        var prevCurrent = CultureInfo.CurrentCulture;
        var prevUi = CultureInfo.CurrentUICulture;
        try
        {
            var it = CultureInfo.GetCultureInfo("it-IT");
            Strings.Culture = it;
            CultureInfo.CurrentCulture = it;
            CultureInfo.CurrentUICulture = it;

            var remote = new AttachmentViewModel(new Recordum.Core.Models.Attachment
            {
                Id = Guid.NewGuid(),
                NoteId = Guid.NewGuid(),
                Filename = "a.jpg",
                MimeType = "image/jpeg",
                HasContent = false,
                OriginDeviceName = "Telefono",
            }, [1]);
            remote.OriginalElsewhereText.Should().Be("Originale su Telefono");

            var local = new AttachmentViewModel(new Recordum.Core.Models.Attachment
            {
                Id = Guid.NewGuid(),
                NoteId = Guid.NewGuid(),
                Filename = "b.jpg",
                MimeType = "image/jpeg",
                HasContent = true,
                OriginDeviceName = "Telefono",
            }, [1]);
            local.OriginalElsewhereText.Should().BeNull();

            var unknownOrigin = new AttachmentViewModel(new Recordum.Core.Models.Attachment
            {
                Id = Guid.NewGuid(),
                NoteId = Guid.NewGuid(),
                Filename = "c.jpg",
                MimeType = "image/jpeg",
                HasContent = false,
                OriginDeviceName = null,
            }, [1]);
            unknownOrigin.OriginalElsewhereText.Should().Be("Originale su un altro dispositivo");

            var missing = new AttachmentViewModel(new Recordum.Core.Models.Attachment
            {
                Id = Guid.NewGuid(),
                NoteId = Guid.NewGuid(),
                Filename = "d.jpg",
                MimeType = "image/jpeg",
                HasContent = false,
                IsMissing = true,
                OriginDeviceName = "Telefono",
            }, null);
            missing.OriginalElsewhereText.Should().BeNull();
        }
        finally
        {
            Strings.Culture = prevStrings;
            CultureInfo.CurrentCulture = prevCurrent;
            CultureInfo.CurrentUICulture = prevUi;
        }
    }
}
