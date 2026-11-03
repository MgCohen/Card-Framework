using System.Collections.Generic;
using SpikeBoth.Notes.Slices.CreateNote;

namespace SpikeBoth.Billing.Slices.OpenInvoiceOnNoteCreated
{
    public sealed class NoteCreatedHandler
    {
        readonly IDictionary<string, string> invoiceToNote;

        public NoteCreatedHandler(IDictionary<string, string> invoiceToNote) => this.invoiceToNote = invoiceToNote;

        public string Handle(NoteCreatedEvent created)
        {
            var invoiceId = "inv-" + created.NoteId;
            invoiceToNote.Add(invoiceId, created.NoteId);
            return invoiceId;
        }
    }
}
