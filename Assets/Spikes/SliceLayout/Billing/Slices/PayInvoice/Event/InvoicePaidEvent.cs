namespace Spike.Billing.Slices.PayInvoice
{
    public sealed record InvoicePaidEvent(string InvoiceId, string NoteId);
}
