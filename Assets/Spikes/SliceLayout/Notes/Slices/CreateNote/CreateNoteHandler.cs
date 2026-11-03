using System;
using System.Collections.Generic;
using Spike.Notes.Contracts.Requests;
using Spike.Notes.Domain;

namespace Spike.Notes.Slices.CreateNote
{
    public sealed class CreateNoteHandler
    {
        readonly IDictionary<NoteId, Note> notes;

        public CreateNoteHandler(IDictionary<NoteId, Note> notes) => this.notes = notes;

        public (CreateNoteResponse Response, NoteCreatedEvent Event) Handle(CreateNoteRequest request)
        {
            if (!CreateNoteValidator.IsValid(request)) throw new ArgumentException("Title is empty.", nameof(request));

            var note = new Note(new NoteId(Guid.NewGuid().ToString("N")), request.Title);
            notes.Add(note.Id, note);
            return (new CreateNoteResponse(new NoteDto(note.Id.Value, note.Title)),
                    new NoteCreatedEvent(note.Id.Value, note.Title));
        }
    }
}
