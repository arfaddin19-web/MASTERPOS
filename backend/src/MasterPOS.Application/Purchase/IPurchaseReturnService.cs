namespace MasterPOS.Application.Purchase;

public interface IPurchaseReturnService
{
    Task<PurchaseReturnDto> CreateAsync(CreatePurchaseReturnRequest request, CancellationToken ct = default);
    Task<PurchaseReturnDto> GetAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<PurchaseReturnDto>> ListAsync(string? status = null, CancellationToken ct = default);

    Task<PurchaseReturnDto> AddLineAsync(Guid returnId, AddPurchaseReturnLineRequest request, CancellationToken ct = default);
    Task<PurchaseReturnDto> UpdateLineAsync(Guid returnId, Guid lineId, UpdatePurchaseReturnLineRequest request, CancellationToken ct = default);
    Task<PurchaseReturnDto> RemoveLineAsync(Guid returnId, Guid lineId, CancellationToken ct = default);

    /// <summary>Locks the return and writes a stock-OUT entry for every line.</summary>
    Task<PurchaseReturnDto> PostAsync(Guid returnId, CancellationToken ct = default);
    Task<PurchaseReturnDto> CancelAsync(Guid returnId, CancellationToken ct = default);
}
