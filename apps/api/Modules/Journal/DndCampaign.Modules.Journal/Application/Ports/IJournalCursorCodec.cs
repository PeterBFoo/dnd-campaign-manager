namespace DndCampaign.Modules.Journal.Application.Ports;

internal interface IJournalCursorCodec
{
    string Encode(JournalPageCursor cursor);

    bool TryDecode(string value, out JournalPageCursor? cursor);
}
