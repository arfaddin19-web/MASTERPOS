using MasterPOS.Application.Common;
using MasterPOS.Domain.Workforce;
using MasterPOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MasterPOS.Application.Workforce;

public class PayrollSettingsService : IPayrollSettingsService
{
    private readonly MasterPosDbContext _db;
    private readonly ICurrentUserContext _currentUser;

    public PayrollSettingsService(MasterPosDbContext db, ICurrentUserContext currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<PayrollSettingsDto> GetAsync(CancellationToken ct = default)
        => ToDto(await GetOrCreateAsync(ct));

    public async Task<PayrollSettingsDto> UpdateAsync(UpdatePayrollSettingsRequest request, CancellationToken ct = default)
    {
        ValidatePercent(request.OvertimeMultiplier, "Overtime multiplier");
        ValidatePercent(request.PfEmployeePercent, "PF employee %");
        ValidatePercent(request.PfEmployerPercent, "PF employer %");
        ValidatePercent(request.SsfEmployeePercent, "SSF employee %");
        ValidatePercent(request.SsfEmployerPercent, "SSF employer %");
        ValidatePercent(request.FestivalBonusPercent, "Festival bonus %");

        var settings = await GetOrCreateAsync(ct);
        settings.OvertimeEnabled = request.OvertimeEnabled;
        settings.OvertimeMultiplier = request.OvertimeMultiplier;
        settings.PfEnabled = request.PfEnabled;
        settings.PfEmployeePercent = request.PfEmployeePercent;
        settings.PfEmployerPercent = request.PfEmployerPercent;
        settings.SsfEnabled = request.SsfEnabled;
        settings.SsfEmployeePercent = request.SsfEmployeePercent;
        settings.SsfEmployerPercent = request.SsfEmployerPercent;
        settings.TdsEnabled = request.TdsEnabled;
        settings.FestivalBonusEnabled = request.FestivalBonusEnabled;
        settings.FestivalBonusPercent = request.FestivalBonusPercent;
        await _db.SaveChangesAsync(ct);

        return ToDto(settings);
    }

    private static void ValidatePercent(decimal value, string label)
    {
        if (value < 0)
            throw new AppException($"{label} can't be negative.");
    }

    private async Task<PayrollSettings> GetOrCreateAsync(CancellationToken ct)
    {
        var settings = await _db.PayrollSettings.SingleOrDefaultAsync(
            s => s.CompanyId == _currentUser.CompanyId && !s.IsDeleted, ct);
        if (settings is not null) return settings;

        // No explicit "initialize" step in the Setup wizard — the first
        // GET (or PUT) for a company just creates the defaults-only row.
        settings = new PayrollSettings { CompanyId = _currentUser.CompanyId };
        _db.PayrollSettings.Add(settings);
        await _db.SaveChangesAsync(ct);
        return settings;
    }

    private static PayrollSettingsDto ToDto(PayrollSettings s) => new(
        s.OvertimeEnabled, s.OvertimeMultiplier,
        s.PfEnabled, s.PfEmployeePercent, s.PfEmployerPercent,
        s.SsfEnabled, s.SsfEmployeePercent, s.SsfEmployerPercent,
        s.TdsEnabled,
        s.FestivalBonusEnabled, s.FestivalBonusPercent);
}
