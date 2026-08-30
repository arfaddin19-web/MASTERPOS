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

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

// Exposed so integration tests can spin the app up via WebApplicationFactory<Program>.
public partial class Program { }
