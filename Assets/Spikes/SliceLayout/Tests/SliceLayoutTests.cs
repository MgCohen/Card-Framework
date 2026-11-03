using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Spike.Billing.Slices.OpenInvoiceOnNoteCreated;
using Spike.Billing.Slices.PayInvoice;
using Spike.Notes.Contracts.Requests;
using Spike.Notes.Domain;
using Spike.Notes.Slices.ArchiveNoteOnInvoicePaid;
using Spike.Notes.Slices.CreateNote;

namespace Spike.SliceLayout.Tests
{
    public class SliceLayoutTests
    {
        static string AssemblyOf(Type type) => type.Assembly.GetName().Name;

        // A slice folder holds files of three assemblies; each .asmref subfolder decides one.
        [TestCase(typeof(CreateNoteRequest), "Spike.Notes.Contracts.Requests")]
        [TestCase(typeof(CreateNoteResponse), "Spike.Notes.Contracts.Requests")]
        [TestCase(typeof(NoteDto), "Spike.Notes.Contracts.Requests")]
        [TestCase(typeof(NoteCreatedEvent), "Spike.Notes.Contracts.Events")]
        [TestCase(typeof(CreateNoteHandler), "Spike.Notes.Slices")]
        [TestCase(typeof(InvoicePaidHandler), "Spike.Notes.Slices")]
        [TestCase(typeof(InvoicePaidEvent), "Spike.Billing.Contracts.Events")]
        [TestCase(typeof(PayInvoiceHandler), "Spike.Billing.Slices")]
        [TestCase(typeof(NoteCreatedHandler), "Spike.Billing.Slices")]
        public void Type_compiles_into_the_assembly_of_its_folder(Type type, string expected)
        {
            Assert.That(AssemblyOf(type), Is.EqualTo(expected));
        }

        [Test]
        public void Slice_private_helper_stays_internal_to_the_slice_assembly()
        {
            var validator = typeof(CreateNoteHandler).Assembly.GetType("Spike.Notes.Slices.CreateNote.CreateNoteValidator");
            Assert.That(validator, Is.Not.Null);
            Assert.That(validator.IsPublic, Is.False);
        }

        [TestCase(typeof(CreateNoteRequest))]
        [TestCase(typeof(NoteCreatedEvent))]
        public void Contract_assembly_references_no_other_module_assembly(Type contract)
        {
            var references = contract.Assembly.GetReferencedAssemblies().Select(a => a.Name).ToArray();
            Assert.That(references, Has.None.StartsWith("Spike."));
        }

        [Test]
        public void Two_modules_react_to_each_others_events()
        {
            var notes = new Dictionary<NoteId, Note>();
            var invoices = new Dictionary<string, string>();

            var (response, created) = new CreateNoteHandler(notes).Handle(new CreateNoteRequest("Hello"));
            var invoiceId = new NoteCreatedHandler(invoices).Handle(created);
            var paid = new PayInvoiceHandler(invoices).Handle(invoiceId);
            new InvoicePaidHandler(notes).Handle(paid);

            Assert.That(response.Note.Title, Is.EqualTo("Hello"));
            Assert.That(notes[new NoteId(response.Note.Id)].Archived, Is.True);
        }
    }
}
