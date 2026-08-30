using MasterPOS.Domain.Common;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace MasterPOS.Infrastructure.Persistence.Configurations;

/// <summary>
/// Most enums convert to/from the database via the default
/// <c>HasConversion&lt;string&gt;()</c> — the enum member name IS the exact
/// string the CHECK constraint expects (verified against every 0*.sql file).
/// The two exceptions, where the natural C# member name doesn't match the
/// database's literal exactly, get an explicit converter here instead of
/// leaning on SQL Server's case-insensitive collation to paper over it.
/// </summary>
public static class EnumConverters
{
    public static readonly ValueConverter<TaxRegistrationType, string> TaxRegistrationType = new(
        v => v == Domain.Common.TaxRegistrationType.Vat ? "VAT" : "PAN",
        v => v == "VAT" ? Domain.Common.TaxRegistrationType.Vat : Domain.Common.TaxRegistrationType.Pan);

    public static readonly ValueConverter<PaymentMode, string> PaymentMode = new(
        v => v == Domain.Common.PaymentMode.ESewa ? "eSewa" : v.ToString(),
        v => v == "eSewa" ? Domain.Common.PaymentMode.ESewa : Enum.Parse<Domain.Common.PaymentMode>(v));
}
