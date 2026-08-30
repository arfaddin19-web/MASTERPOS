using MasterPOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

// MasterPOS.Migrator — applies pending EF Core migrations against a real
// SQL Server database using nothing but the .NET runtime this exe was
// published with. It exists so a packaged install on a client's machine
// can create/update its schema without the `dotnet-ef` CLI tool (which
// isn't part of a normal .NET install) — installer\MasterPOS.iss runs this
// once, right after copying the published API and before the Windows
// Service starts. It's also just `dotnet run` away for local development
// against a real database, same as `dotnet ef database update` was.

var connectionString = args.Length > 0 ? args[0]
    : Environment.GetEnvironmentVariable("MASTERPOS_CONNECTION_STRING")
    ?? throw new InvalidOperationException(
        "Pass the connection string as the first argument, or set the " +
        "MASTERPOS_CONNECTION_STRING environment variable.");

Console.WriteLine("MasterPOS.Migrator — checking the database schema...");

var optionsBuilder = new DbContextOptionsBuilder<MasterPosDbContext>();
optionsBuilder.UseSqlServer(connectionString);

using var db = new MasterPosDbContext(optionsBuilder.Options);

try
{
    var pending = (await db.Database.GetPendingMigrationsAsync()).ToList();
    if (pending.Count == 0)
    {
        Console.WriteLine("Database is already up to date — nothing to do.");
        return 0;
    }

    Console.WriteLine($"Applying {pending.Count} pending migration(s): {string.Join(", ", pending)}");
    await db.Database.MigrateAsync();
    Console.WriteLine("Done — schema is up to date.");
    return 0;
}
catch (Exception ex)
{
    // A clear, one-line cause on the console (and in the installer's log)
    // beats a raw .NET stack trace when this fails on-site — most failures
    // here are "SQL Server isn't reachable yet" or "the connection string
    // is wrong", not a real bug.
    Console.Error.WriteLine($"Migration failed: {ex.Message}");
    return 1;
}
