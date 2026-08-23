using DndCampaign.Modules.Journal.Application.Ports;
using DndCampaign.Modules.Journal.Infrastructure.Pagination;
using Xunit;

namespace DndCampaign.Modules.Journal.Tests.Infrastructure;

public sealed class JournalCursorCodecTests
{
    [Fact]
    public void Cursor_round_trips_without_public_identifiers()
    {
        var codec = new JournalCursorCodec();
        var expected = new JournalPageCursor(DateTimeOffset.Parse("2026-08-23T10:00:00Z"), 42);

        var encoded = codec.Encode(expected);
        var valid = codec.TryDecode(encoded, out var decoded);

        Assert.True(valid);
        Assert.Equal(expected, decoded);
        Assert.DoesNotContain("2026", encoded, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-cursor")]
    public void Invalid_cursor_is_rejected(string value)
    {
        Assert.False(new JournalCursorCodec().TryDecode(value, out _));
    }
}
