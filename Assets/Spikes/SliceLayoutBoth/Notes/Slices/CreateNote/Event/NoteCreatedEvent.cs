namespace SpikeBoth.Notes.Slices.CreateNote
{
    public sealed record NoteCreatedEvent(string NoteId, string Title);
}
