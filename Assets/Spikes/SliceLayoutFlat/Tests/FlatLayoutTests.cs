using System;
using System.Linq;
using NUnit.Framework;

namespace SpikeFlat.Tests
{
    public class FlatLayoutTests
    {
        static Type Find(string fullName) =>
            AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(fullName)).FirstOrDefault(t => t != null);

        // No asmdef and no asmref: every slice file goes into Assembly-CSharp.
        [TestCase("SpikeFlat.Notes.Slices.CreateNote.CreateNoteRequest")]
        [TestCase("SpikeFlat.Notes.Slices.CreateNote.NoteCreatedEvent")]
        [TestCase("SpikeFlat.Notes.Slices.CreateNote.CreateNoteHandler")]
        [TestCase("SpikeFlat.Notes.Contracts.Requests.NoteDto")]
        [TestCase("SpikeFlat.Notes.Domain.NoteId")]
        [TestCase("SpikeFlat.Billing.Slices.PayInvoice.InvoicePaidEvent")]
        [TestCase("SpikeFlat.Notes.Slices.CreateNote.LeakDomainRequest")]
        [TestCase("SpikeFlat.Notes.Slices.CreateNote.LeakHandlerRequest")]
        [TestCase("SpikeFlat.Notes.Slices.CreateNote.LeakInternalRequest")]
        public void Type_compiles_into_Assembly_CSharp(string fullName)
        {
            var type = Find(fullName);
            Assert.That(type, Is.Not.Null, fullName + " was not compiled.");
            Assert.That(type.Assembly.GetName().Name, Is.EqualTo("Assembly-CSharp"));
        }
    }
}
