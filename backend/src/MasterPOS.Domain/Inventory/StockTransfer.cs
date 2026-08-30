using MasterPOS.Domain.Common;
using MasterPOS.Domain.Masters;

namespace MasterPOS.Domain.Inventory;

/// <summary>Matches the "Quick Stock Transfer" form — one product, one
/// From/To warehouse pair. Posting one writes a TransferOut row at the
/// source and a TransferIn row at the destination in StockLedgerEntry.</summary>
public class StockTransfer : CompanyOwnedEntity
{
    public Guid ProductId { get; set; }
    public Guid FromWarehouseId { get; set; }
    public Guid ToWarehouseId { get; set; }
    public decimal Quantity { get; set; }
    public DateOnly TransferDate { get; set; }
    public StockTransferStatus Status { get; set; } = StockTransferStatus.Completed;

    public Product Product { get; set; } = null!;
    public Warehouse FromWarehouse { get; set; } = null!;
    public Warehouse ToWarehouse { get; set; } = null!;
}
