using System.Collections.Generic;

namespace SpikeFlat.Billing.Slices.PayInvoice
{
    public sealed class PayInvoiceHandler
    {
        readonly IDictionary<string, string> invoiceToNote;

        public PayInvoiceHandler(IDictionary<string, string> invoiceToNote) => this.invoiceToNote = invoiceToNote;

        public InvoicePaidEvent Handle(string invoiceId) => new InvoicePaidEvent(invoiceId, invoiceToNote[invoiceId]);
    }
}
