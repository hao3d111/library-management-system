using System.Security.Claims;
using System.Text;
using System.Text.Json;
using LMS.Business;
using LMS.Data;
using LMS.Data.Context;
using LMS.Data.Seed;
using LMS.WebApi.Middleware;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;

var physicalWwwRoot = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
var contentRoot = Directory.Exists(physicalWwwRoot)
    ? Directory.GetCurrentDirectory()
    : AppContext.BaseDirectory;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = contentRoot,
    WebRootPath = Path.Combine(contentRoot, "wwwroot")
});

// Provide safe built-in defaults so a standalone AthenaeumLMS.exe runs even without external appsettings*.json
var defaultProvider = builder.Environment.IsDevelopment() ? "SqlServer" : "Auto";
builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
{
    ["ConnectionStrings:DefaultConnection"] = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? "Server=.\\SQLEXPRESS;Database=LibraryManagementDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True;",
    ["DatabaseProvider"] = builder.Configuration["DatabaseProvider"] ?? defaultProvider,
    ["Sqlite:FileName"] = builder.Configuration["Sqlite:FileName"] ?? "library.db",
    ["Jwt:SecretKey"] = builder.Configuration["Jwt:SecretKey"]
        ?? "LMS_SuperSecret_SigningKey_ChangeInProduction_2026_SecureToken_9876543210!",
    ["Jwt:Issuer"] = builder.Configuration["Jwt:Issuer"] ?? "LMS.WebApi",
    ["Jwt:Audience"] = builder.Configuration["Jwt:Audience"] ?? "LMS.Client",
    ["Jwt:ExpiryMinutes"] = builder.Configuration["Jwt:ExpiryMinutes"] ?? "480"
});

if (string.IsNullOrWhiteSpace(builder.Configuration["Urls"]) &&
    string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ASPNETCORE_URLS")))
{
    builder.WebHost.UseUrls("http://localhost:5080");
}

static string ResolvePortableDataDirectory()
{
    var processPath = Environment.ProcessPath;
    if (!string.IsNullOrWhiteSpace(processPath))
    {
        var exeName = Path.GetFileNameWithoutExtension(processPath);
        if (!exeName.Equals("dotnet", StringComparison.OrdinalIgnoreCase) &&
            !exeName.Equals("testhost", StringComparison.OrdinalIgnoreCase))
        {
            var exeDir = Path.GetDirectoryName(processPath);
            if (!string.IsNullOrWhiteSpace(exeDir))
            {
                return exeDir;
            }
        }
    }
    return Directory.GetCurrentDirectory();
}

var configuredProvider = builder.Configuration["DatabaseProvider"]
    ?? Environment.GetEnvironmentVariable("LMS_DB_PROVIDER")
    ?? defaultProvider;

var explicitSqlitePath = builder.Configuration["Sqlite:FilePath"];
var sqliteFileName = builder.Configuration["Sqlite:FileName"] ?? "library.db";
var sqlitePath = !string.IsNullOrWhiteSpace(explicitSqlitePath)
    ? explicitSqlitePath
    : Path.Combine(ResolvePortableDataDirectory(), sqliteFileName);

var sqlServerConnectionString = builder.Configuration.GetConnectionString("DefaultConnection");
var dbInfo = DataServiceExtensions.ResolveDatabaseConfiguration(
    configuredProvider,
    sqlServerConnectionString,
    sqlitePath);

builder.Services.AddDataAccessLayer(dbInfo);
builder.Services.AddBusinessLogicLayer();

builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        options.SuppressModelStateInvalidFilter = false;
    });

var jwtSecret = builder.Configuration["Jwt:SecretKey"]!;
var jwtIssuer = builder.Configuration["Jwt:Issuer"]!;
var jwtAudience = builder.Configuration["Jwt:Audience"]!;

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtIssuer,
        ValidAudience = jwtAudience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
        RoleClaimType = ClaimTypes.Role,
        NameClaimType = ClaimTypes.Name
    };

    options.Events = new JwtBearerEvents
    {
        OnChallenge = async context =>
        {
            context.HandleResponse();
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(new
            {
                status = 401,
                message = "Authentication is required to access this resource."
            }));
        },
        OnForbidden = async context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(new
            {
                status = 403,
                message = "You do not have permission to perform this action."
            }));
        }
    };
});

builder.Services.AddAuthorization();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<LibraryDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    if (dbInfo.Provider == DatabaseProviderType.Sqlite)
    {
        if (dbInfo.UsedSqliteFallback)
        {
            logger.LogWarning(
                "SQL Server was unavailable ({Reason}). Falling back to portable SQLite database at {Path}",
                dbInfo.FallbackReason,
                dbInfo.DisplayTarget);
        }
        else
        {
            logger.LogInformation("Using portable SQLite database at {Path}", dbInfo.DisplayTarget);
        }

        await db.Database.EnsureCreatedAsync();
    }
    else
    {
        logger.LogInformation("Using SQL Server database ({Target})", dbInfo.DisplayTarget);
        await db.Database.MigrateAsync();
    }

    await DatabaseSeeder.SeedAsync(db);
}

app.UseMiddleware<ExceptionHandlingMiddleware>();

// Serve frontend from physical wwwroot if index.html exists on disk, otherwise from embedded resources inside the single-file .exe
var physicalWebRootDir = Path.Combine(contentRoot, "wwwroot");
IFileProvider staticFileProvider = File.Exists(Path.Combine(physicalWebRootDir, "index.html"))
    ? new PhysicalFileProvider(physicalWebRootDir)
    : new ManifestEmbeddedFileProvider(typeof(Program).Assembly, "wwwroot");

app.UseDefaultFiles(new DefaultFilesOptions
{
    FileProvider = staticFileProvider
});
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = staticFileProvider
});

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program { }
