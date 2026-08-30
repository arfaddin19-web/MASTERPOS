using MasterPOS.Application.Common;
using MasterPOS.Domain.Common;
using MasterPOS.Domain.Utility;
using MasterPOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MasterPOS.Application.Utility;

public class PaymentModeSettingService : IPaymentModeSettingService
{
    private static readonly string[] AllModes = Enum.GetValues<PaymentMode>().Select(m => m.ToString()).ToArray();
    // Sensible defaults for a fresh install — the two universally-available
    // modes on, the rest left for the admin to switch on once set up.
    private static readonly HashSet<string> DefaultEnabled = new() { nameof(PaymentMode.Cash), nameof(PaymentMode.Card) };

    private readonly MasterPosDbContext _db;
    private readonly ICurrentUserContext _currentUser;

    public PaymentModeSettingService(MasterPosDbContext db, ICurrentUserContext currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<PaymentModeSettingDto>> ListAsync(CancellationToken ct = default)
    {
        await EnsureSeededAsync(ct);
        var modes = await _db.PaymentModeSettings
            .Where(m => m.CompanyId == _currentUser.CompanyId)
            .OrderBy(m => m.Code)
            .ToListAsync(ct);
        return modes.Select(ToDto).ToList();
    }

    public async Task<PaymentModeSettingDto> SetEnabledAsync(string code, SetPaymentModeEnabledRequest request, CancellationToken ct = default)
    {
        await EnsureSeededAsync(ct);
        var mode = await _db.PaymentModeSettings.SingleOrDefaultAsync(
            m => m.CompanyId == _currentUser.CompanyId && m.Code == code, ct)
            ?? throw new AppException($"Unknown payment mode '{code}'.");

        mode.IsEnabled = request.IsEnabled;
        await _db.SaveChangesAsync(ct);
        return ToDto(mode);
    }

    private async Task EnsureSeededAsync(CancellationToken ct)
    {
        var existing = await _db.PaymentModeSettings
            .Where(m => m.CompanyId == _currentUser.CompanyId)
            .Select(m => m.Code)
            .ToListAsync(ct);
        var missing = AllModes.Except(existing);
        foreach (var code in missing)
            _db.PaymentModeSettings.Add(new PaymentModeSetting
            {
                CompanyId = _currentUser.CompanyId, Code = code, IsEnabled = DefaultEnabled.Contains(code),
            });
        if (existing.Count < AllModes.Length)
            await _db.SaveChangesAsync(ct);
    }

    private static PaymentModeSettingDto ToDto(PaymentModeSetting m) => new(m.Id, m.Code, m.IsEnabled);
}
