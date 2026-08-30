namespace MasterPOS.Domain.Utility;

/// <summary>
/// Maps to table Utility.PaymentModes — named PaymentModeSetting in C# so
/// it can't be confused with the Common.PaymentMode enum (the value used
/// on an actual OrderPayment/PartyPayment). This table is just which modes
/// a company has switched on, matching Settings → Payment Modes.
/// </summary>
public class PaymentModeSetting
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }

    /// <summary>The enum name as text — Cash, Card, eSewa, Khalti,
    /// BankTransfer. Kept as a plain string here (not the enum) because
    /// this row's whole purpose is enabling/disabling one, independent of
    /// any single transaction.</summary>
    public string Code { get; set; } = null!;
    public bool IsEnabled { get; set; } = true;
}
