namespace MasterPOS.Application.Purchase;

public interface IPurchaseInvoiceService
{
    Task<PurchaseInvoiceDto> CreateAsync(CreatePurchaseInvoiceRequest request, CancellationToken ct = default);
    Task<PurchaseInvoiceDto> GetAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<PurchaseInvoiceDto>> ListAsync(string? status = null, CancellationToken ct = default);

    Task<PurchaseInvoiceDto> AddLineAsync(Guid invoiceId, AddPurchaseInvoiceLineRequest request, CancellationToken ct = default);
    Task<PurchaseInvoiceDto> UpdateLineAsync(Guid invoiceId, Guid lineId, UpdatePurchaseInvoiceLineRequest request, CancellationToken ct = default);
    Task<PurchaseInvoiceDto> RemoveLineAsync(Guid invoiceId, Guid lineId, CancellationToken ct = default);

    /// <summary>Locks the invoice and writes a stock-IN entry for every line — the invoice's
    /// Draft → Posted transition is the moment the goods are treated as received.</summary>
    Task<PurchaseInvoiceDto> PostAsync(Guid invoiceId, CancellationToken ct = default);

    /// <summary>Draft only — a Posted invoice has already moved stock, so a Purchase Return
    /// is the correct way to reverse it, not cancellation.</summary>
    Task<PurchaseInvoiceDto> CancelAsync(Guid invoiceId, CancellationToken ct = default);

    /// <summary>Increments the invoice's own running AmountPaid. The corresponding
    /// Accounting.PartyPayments ledger entry is that (not-yet-built) module's job.</summary>
    Task<PurchaseInvoiceDto> RecordPaymentAsync(Guid invoiceId, RecordPurchasePaymentRequest request, CancellationToken ct = default);
}
