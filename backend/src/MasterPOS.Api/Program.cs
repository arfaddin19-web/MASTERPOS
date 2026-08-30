using System.IdentityModel.Tokens.Jwt;
using System.Text;
using MasterPOS.Api.Auth;
using MasterPOS.Application;
using MasterPOS.Application.Auth;
using MasterPOS.Application.Common;
using MasterPOS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

// ASP.NET Core's JWT handler otherwise silently remaps well-known claims on
// validation — "sub" becomes the long ClaimTypes.NameIdentifier URI — so
// HttpCurrentUserContext's FindFirst("sub") would never match what
// JwtTokenService actually put on the token. Disabling this keeps the
// claims exactly as issued on both sides.
JwtSecurityTokenHandler.DefaultMapInboundClaims = false;

var builder = WebApplication.CreateBuilder(args);

// Lets `sc.exe create` register this exe as a real Windows Service on a
// packaged client install (installer\MasterPOS.iss does that registration —
// this just makes the app speak the Windows Service Control Manager
// protocol when it's actually launched as one). A no-op everywhere else —
// `dotnet run`, the test suite, Docker — so safe to call unconditionally.
builder.Host.UseWindowsService(options => options.ServiceName = "MasterPOS");

// installer\MasterPOS.iss writes the per-client connection string, JWT
// signing key, and backup directory here at install time — appsettings.json
// itself stays a committed placeholder (see its own _comment_* keys). Added
// after CreateBuilder's own default chain, so this is the highest-priority
// source of all (beats appsettings.{Environment}.json *and* env vars) —
// deliberately: it's the one file an on-site install is meant to hold the
// real answer in, so an admin edits it directly rather than an env var
// quietly overriding what it says.
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: false);

// Local-server-per-client model: the connection string comes from
// appsettings (per-install, edited during on-site setup) or an env var —
// never hardcoded, since every client's SQL Server instance differs.
var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? Environment.GetEnvironmentVariable("MASTERPOS_CONNECTION_STRING")
    ?? throw new InvalidOperationException(
        "No database connection string configured. Set ConnectionStrings:Default " +
        "in appsettings.json or the MASTERPOS_CONNECTION_STRING environment variable.");

builder.Services.AddDbContext<MasterPosDbContext>(options => options.UseSqlServer(connectionString));

builder.Services.AddApplication();
builder.Services.AddScoped<ITokenService, JwtTokenService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserContext, HttpCurrentUserContext>();

var jwtSection = builder.Configuration.GetSection("Jwt");
var signingKey = jwtSection["SigningKey"]
    ?? throw new InvalidOperationException(
        "No Jwt:SigningKey configured. Every install must set its own — see appsettings.json.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtSection["Issuer"],
            ValidateAudience = true,
            ValidAudience = jwtSection["Audience"],
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "MasterPOS API", Version = "v1" });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Paste just the token — Swagger adds the \"Bearer \" prefix.",
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" },
            },
            Array.Empty<string>()
        },
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// No HTTPS redirection: every install runs on the client's own local
// network with no public exposure and no internet-issued certificate to
// redirect to (see README "Deployment model") — the same reasoning that
// already put auth on a bearer JWT instead of a cookie.
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// A packaged install (installer\publish.ps1) copies the built frontend
// (frontend/dist) into wwwroot before publishing, so this one process
// serves both the API and the UI on one port — the client opens a single
// URL, nothing else to run. In dev (`npm run dev`) wwwroot doesn't exist —
// Vite serves the frontend separately instead — so this is skipped rather
// than erroring on a missing directory. The fallback pattern excludes
// "api/*" explicitly — MapFallbackToFile alone only catches requests
// MapControllers didn't already match, which includes a *mistyped* /api/...
// path (there's no controller route for it either); without the exclusion
// that would silently return index.html instead of a real 404.
var webRootPath = Path.Combine(app.Environment.ContentRootPath, "wwwroot");
if (Directory.Exists(webRootPath))
{
    app.UseDefaultFiles();
    app.UseStaticFiles();
    app.MapFallbackToFile("{*path:regex(^(?!api/).*$)}", "index.html");
}

app.Run();

// Exposed so integration tests can spin the app up via WebApplicationFactory<Program>.
public partial class Program { }
