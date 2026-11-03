namespace Spike.Notes.Domain
{
    public sealed class Note
    {
        public Note(NoteId id, string title)
        {
            Id = id;
            Title = title;
        }

        public NoteId Id { get; }
        public string Title { get; }
        public bool Archived { get; private set; }

        public void Archive() => Archived = true;
    }
}
