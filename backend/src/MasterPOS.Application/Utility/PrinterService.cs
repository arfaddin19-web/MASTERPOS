using MasterPOS.Application.Common;
using MasterPOS.Domain.Common;
using MasterPOS.Domain.Utility;
using MasterPOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MasterPOS.Application.Utility;

public class PrinterService : IPrinterService
{
    private readonly MasterPosDbContext _db;
    private readonly ICurrentUserContext _currentUser;

    public PrinterService(MasterPosDbContext db, ICurrentUserContext currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<PrinterDto> CreateAsync(UpsertPrinterRequest request, CancellationToken ct = default)
    {
        var (printerType, station) = await ValidateAsync(request, ct);
        var printer = new Printer
        {
            CompanyId = _currentUser.CompanyId,
            BranchId = request.BranchId,
            Name = request.Name,
            PrinterType = printerType,
            Station = station,
            ConnectionInfo = request.ConnectionInfo,
            IsEnabled = request.IsEnabled,
        };
        _db.Printers.Add(printer);
        await _db.SaveChangesAsync(ct);
        return ToDto(await GetOwnedAsync(printer.Id, ct));
    }

    public async Task<PrinterDto> UpdateAsync(Guid id, UpsertPrinterRequest request, CancellationToken ct = default)
    {
        var (printerType, station) = await ValidateAsync(request, ct);
        var printer = await GetOwnedAsync(id, ct);
        printer.BranchId = request.BranchId;
        printer.Name = request.Name;
        printer.PrinterType = printerType;
        printer.Station = station;
        printer.ConnectionInfo = request.ConnectionInfo;
        printer.IsEnabled = request.IsEnabled;
        await _db.SaveChangesAsync(ct);
        return ToDto(await GetOwnedAsync(id, ct));
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var printer = await GetOwnedAsync(id, ct);
        printer.IsDeleted = true;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<PrinterDto>> ListAsync(Guid? branchId = null, CancellationToken ct = default)
    {
        var query = _db.Printers
            .Include(p => p.Branch)
            .Where(p => p.CompanyId == _currentUser.CompanyId && !p.IsDeleted);
        if (branchId is { } b) query = query.Where(p => p.BranchId == b);

        var printers = await query.OrderBy(p => p.Name).ToListAsync(ct);
        return printers.Select(ToDto).ToList();
    }

    private async Task<(PrinterType Type, KotStation? Station)> ValidateAsync(UpsertPrinterRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new AppException("Name is required.");
        if (!Enum.TryParse<PrinterType>(request.PrinterType, ignoreCase: true, out var printerType))
            throw new AppException($"Unknown printer type '{request.PrinterType}'.");

        KotStation? station = null;
        if (!string.IsNullOrWhiteSpace(request.Station))
        {
            if (!Enum.TryParse<KotStation>(request.Station, ignoreCase: true, out var parsed))
                throw new AppException($"Unknown station '{request.Station}'.");
            station = parsed;
        }
        if (printerType != PrinterType.Kot && station is not null)
            throw new AppException("Station only applies to a Kot printer.");

        if (!await _db.Branches.AnyAsync(b => b.Id == request.BranchId && b.CompanyId == _currentUser.CompanyId && !b.IsDeleted, ct))
            throw new AppException("The selected branch does not exist.");

        return (printerType, station);
    }

    private async Task<Printer> GetOwnedAsync(Guid id, CancellationToken ct)
    {
        var printer = await _db.Printers
            .Include(p => p.Branch)
            .SingleOrDefaultAsync(p => p.Id == id && p.CompanyId == _currentUser.CompanyId && !p.IsDeleted, ct);
        return printer ?? throw new AppException("Printer not found.");
    }

    private static PrinterDto ToDto(Printer p) => new(
        p.Id, p.BranchId, p.Branch.Name, p.Name, p.PrinterType.ToString(), p.Station?.ToString(), p.ConnectionInfo, p.IsEnabled);
}
