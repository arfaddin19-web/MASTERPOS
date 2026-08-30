using MasterPOS.Application.Common;
using MasterPOS.Domain.Accounting;
using MasterPOS.Domain.Common;
using MasterPOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MasterPOS.Application.Accounting;

public class PartyPaymentService : IPartyPaymentService
{
    private readonly MasterPosDbContext _db;
    private readonly ICurrentUserContext _currentUser;
    private readonly IAuditLogger _auditLogger;

    public PartyPaymentService(MasterPosDbContext db, ICurrentUserContext currentUser, IAuditLogger auditLogger)
    {
        _db = db;
        _currentUser = currentUser;
        _auditLogger = auditLogger;
    }

    public async Task<PartyPaymentDto> CreateAsync(CreatePartyPaymentRequest request, CancellationToken ct = default)
    {
        var branchId = _currentUser.BranchId
            ?? throw new AppException("Your account has no default branch assigned.");

        if (request.Amount <= 0)
            throw new AppException("Amount must be greater than zero.");
        if (!Enum.TryParse<PartyPaymentDirection>(request.Direction, ignoreCase: true, out var direction))
            throw new AppException($"Unknown direction '{request.Direction}'.");
        if (!Enum.TryParse<PaymentMode>(request.PaymentMode, ignoreCase: true, out var mode))
            throw new AppException($"Unknown payment mode '{request.PaymentMode}'.");

        var party = await _db.Parties.SingleOrDefaultAsync(
            p => p.Id == request.PartyId && p.CompanyId == _currentUser.CompanyId && !p.IsDeleted, ct)
            ?? throw new AppException("The selected party does not exist.");

        PartyPaymentReferenceType? referenceType = null;
        if (!string.IsNullOrWhiteSpace(request.ReferenceType))
        {
            if (!Enum.TryParse<PartyPaymentReferenceType>(request.ReferenceType, ignoreCase: true, out var parsed))
                throw new AppException($"Unknown reference type '{request.ReferenceType}'.");
            if (request.ReferenceId is not { } referenceId)
                throw new AppException("A reference type was given without a reference id.");
            referenceType = parsed;
            await ApplyReferenceSideEffectAsync(parsed, referenceId, party.Id, request.Amount, ct);
        }

        var payment = new PartyPayment
        {
            CompanyId = _currentUser.CompanyId,
            BranchId = branchId,
            PartyId = party.Id,
            Direction = direction,
            Amount = request.Amount,
            PaymentMode = mode,
            ReferenceType = referenceType,
            ReferenceId = referenceType is null ? null : request.ReferenceId,
            PaymentDate = request.PaymentDate,
            Narration = request.Narration,
        };
        _db.PartyPayments.Add(payment);
        await _db.SaveChangesAsync(ct);
        await _auditLogger.LogAsync("Created", "Accounting.PartyPayments", payment.Id,
            $"recorded {direction} payment of Rs. {payment.Amount:0.00} for '{party.Name}'", ct);
        return ToDto(await GetOwnedAsync(payment.Id, ct));
    }

    public async Task<PartyPaymentDto> GetAsync(Guid id, CancellationToken ct = default)
        => ToDto(await GetOwnedAsync(id, ct));

    public async Task<IReadOnlyList<PartyPaymentDto>> ListAsync(Guid? partyId = null, CancellationToken ct = default)
    {
        var query = _db.PartyPayments
            .Include(p => p.Party)
            .Where(p => p.CompanyId == _currentUser.CompanyId && !p.IsDeleted);
        if (partyId is { } id) query = query.Where(p => p.PartyId == id);

        var payments = await query.OrderByDescending(p => p.PaymentDate).ToListAsync(ct);
        return payments.Select(ToDto).ToList();
    }

    /// <summary>A PurchaseInvoice reference advances that invoice's own
    /// AmountPaid so the two views of "how much has been paid" never drift
    /// apart — same balance cap PurchaseInvoiceService.RecordPaymentAsync
    /// itself enforces. A PurchaseReturn/OpeningBalance reference is just a
    /// label for reporting; those documents don't track their own payment
    /// state.</summary>
    private async Task ApplyReferenceSideEffectAsync(
        PartyPaymentReferenceType referenceType, Guid referenceId, Guid partyId, decimal amount, CancellationToken ct)
    {
        if (referenceType != PartyPaymentReferenceType.PurchaseInvoice) return;

        var invoice = await _db.PurchaseInvoices.SingleOrDefaultAsync(
            i => i.Id == referenceId && i.CompanyId == _currentUser.CompanyId && !i.IsDeleted, ct)
            ?? throw new AppException("The referenced purchase invoice does not exist.");
        if (invoice.SupplierId != partyId)
            throw new AppException("The referenced purchase invoice belongs to a different party.");
        if (invoice.Status != DocumentStatus.Posted)
            throw new AppException($"Invoice {invoice.InvoiceNumber} must be Posted before recording a payment against it.");

        var remaining = invoice.GrandTotalAmount - invoice.AmountPaid;
        if (amount > remaining)
            throw new AppException($"Payment of Rs. {amount:0.00} exceeds invoice {invoice.InvoiceNumber}'s remaining balance of Rs. {remaining:0.00}.");

        invoice.AmountPaid += amount;
    }

    private async Task<PartyPayment> GetOwnedAsync(Guid id, CancellationToken ct)
    {
        var payment = await _db.PartyPayments
            .Include(p => p.Party)
            .SingleOrDefaultAsync(p => p.Id == id && p.CompanyId == _currentUser.CompanyId && !p.IsDeleted, ct);
        return payment ?? throw new AppException("Party payment not found.");
    }

    private static PartyPaymentDto ToDto(PartyPayment p) => new(
        p.Id, p.PartyId, p.Party.Name, p.Direction.ToString(), p.Amount, p.PaymentMode.ToString(),
        p.ReferenceType?.ToString(), p.ReferenceId, p.PaymentDate, p.Narration);
}
