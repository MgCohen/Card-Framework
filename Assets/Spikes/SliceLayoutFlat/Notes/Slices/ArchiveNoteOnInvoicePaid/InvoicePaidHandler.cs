using System.Collections.Generic;
using SpikeFlat.Billing.Slices.PayInvoice;
using SpikeFlat.Notes.Domain;

namespace SpikeFlat.Notes.Slices.ArchiveNoteOnInvoicePaid
{
    public sealed class InvoicePaidHandler
    {
        readonly IDictionary<NoteId, Note> notes;

        public InvoicePaidHandler(IDictionary<NoteId, Note> notes) => this.notes = notes;

        public void Handle(InvoicePaidEvent paid)
        {
            if (notes.TryGetValue(new NoteId(paid.NoteId), out var note)) note.Archive();
        }
    }
}
