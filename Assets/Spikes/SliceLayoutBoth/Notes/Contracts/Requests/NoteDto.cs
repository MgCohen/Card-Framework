namespace SpikeBoth.Notes.Contracts.Requests
{
    // A contract that several slices share stays directly in Contracts/Requests/.
    public sealed record NoteDto(string Id, string Title);
}
