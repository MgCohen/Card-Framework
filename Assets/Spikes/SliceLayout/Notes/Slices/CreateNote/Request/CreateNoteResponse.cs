using Spike.Notes.Contracts.Requests;

namespace Spike.Notes.Slices.CreateNote
{
    public sealed record CreateNoteResponse(NoteDto Note);
}
