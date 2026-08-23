using System.Buffers.Binary;
using DndCampaign.Modules.Journal.Application.Ports;
using Microsoft.AspNetCore.WebUtilities;

namespace DndCampaign.Modules.Journal.Infrastructure.Pagination;

internal sealed class JournalCursorCodec : IJournalCursorCodec
{
    private const byte Version = 1;
    private const int PayloadLength = 17;

    public string Encode(JournalPageCursor cursor)
    {
        Span<byte> payload = stackalloc byte[PayloadLength];
        payload[0] = Version;
        BinaryPrimitives.WriteInt64BigEndian(payload[1..9], cursor.CreatedAt.UtcTicks);
        BinaryPrimitives.WriteInt64BigEndian(payload[9..17], cursor.PaginationSequence);
        return WebEncoders.Base64UrlEncode(payload);
    }

    public bool TryDecode(string value, out JournalPageCursor? cursor)
    {
        cursor = null;
        if (string.IsNullOrWhiteSpace(value) || value.Length > 128)
        {
            return false;
        }

        try
        {
            var payload = WebEncoders.Base64UrlDecode(value);
            if (payload.Length != PayloadLength || payload[0] != Version)
            {
                return false;
            }

            var ticks = BinaryPrimitives.ReadInt64BigEndian(payload.AsSpan(1, 8));
            var sequence = BinaryPrimitives.ReadInt64BigEndian(payload.AsSpan(9, 8));
            if (ticks <= 0 || sequence <= 0)
            {
                return false;
            }

            cursor = new JournalPageCursor(new DateTimeOffset(ticks, TimeSpan.Zero), sequence);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }
}
