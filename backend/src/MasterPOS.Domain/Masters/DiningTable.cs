using MasterPOS.Domain.Common;
using MasterPOS.Domain.Core;

namespace MasterPOS.Domain.Masters;

/// <summary>
/// Cafe/Restaurant only (the app hides this whole area for Trading). Status
/// is stored here — not just derived — so the floor plan and the
/// Dashboard's Live Floor Status card are cheap to query; the app updates
/// it whenever an order opens, gets a partial payment, or closes.
/// </summary>
public class DiningTable : CompanyOwnedEntity
{
    public Guid BranchId { get; set; }
    public string TableNumber { get; set; } = null!;
    public string? FloorLabel { get; set; }
    public int Seats { get; set; } = 4;
    public DiningTableStatus Status { get; set; } = DiningTableStatus.Vacant;

    public Branch Branch { get; set; } = null!;
}

public enum DiningTableStatus { Vacant, Occupied, PartiallyPaid }
