using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MasterPOS.Infrastructure.Persistence;

/// <summary>
/// Lets `dotnet ef migrations add ...` run against this project directly,
/// without needing the Api project as the EF Core "startup project". The
/// connection string here is only ever used at design time (generating
/// migrations) — the real one always comes from appsettings/environment at
/// runtime, wired up in MasterPOS.Api's Program.cs.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<MasterPosDbContext>
{
    public MasterPosDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("MASTERPOS_CONNECTION_STRING")
            ?? "Server=localhost;Database=MasterPOS;User Id=sa;Password=Placeholder_ChangeMe!;TrustServerCertificate=True;";

        var optionsBuilder = new DbContextOptionsBuilder<MasterPosDbContext>();
        optionsBuilder.UseSqlServer(connectionString);

        return new MasterPosDbContext(optionsBuilder.Options);
    }
}
