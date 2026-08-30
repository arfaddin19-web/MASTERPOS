using MasterPOS.Domain.Common;
using MasterPOS.Domain.Core;

namespace MasterPOS.Domain.Utility;

public class Printer : CompanyOwnedEntity
{
    public Guid BranchId { get; set; }
    public string Name { get; set; } = null!;
    public PrinterType PrinterType { get; set; }

    /// <summary>Only meaningful when PrinterType == Kot.</summary>
    public KotStation? Station { get; set; }
    public string? ConnectionInfo { get; set; }
    public bool IsEnabled { get; set; } = true;

    public Branch Branch { get; set; } = null!;
}
