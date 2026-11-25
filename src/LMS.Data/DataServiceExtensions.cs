using LMS.Data.Context;
using LMS.Data.Repositories.Implementations;
using LMS.Data.Repositories.Interfaces;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LMS.Data;

public enum DatabaseProviderType
{
    SqlServer,
    Sqlite
}

public sealed record DatabaseConfigurationInfo(
    DatabaseProviderType Provider,
    string ConnectionString,
    string DisplayTarget,
    bool UsedSqliteFallback,
    string? FallbackReason = null);

public static class DataServiceExtensions
{
    public static DatabaseConfigurationInfo ResolveDatabaseConfiguration(
        string? configuredProvider,
        string? sqlServerConnectionString,
        string sqliteFilePath)
    {
        var mode = string.IsNullOrWhiteSpace(configuredProvider)
            ? "SqlServer"
            : configuredProvider.Trim();

        if (mode.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
        {
            return new DatabaseConfigurationInfo(
                DatabaseProviderType.Sqlite,
                $"Data Source={sqliteFilePath}",
                sqliteFilePath,
                UsedSqliteFallback: false);
        }

        if (mode.Equals("Auto", StringComparison.OrdinalIgnoreCase))
        {
            string? probeError = null;
            if (!string.IsNullOrWhiteSpace(sqlServerConnectionString) &&
                TryProbeSqlServer(sqlServerConnectionString, out probeError))
            {
                return new DatabaseConfigurationInfo(
                    DatabaseProviderType.SqlServer,
                    sqlServerConnectionString,
                    ExtractSqlServerTarget(sqlServerConnectionString),
                    UsedSqliteFallback: false);
            }
            else
            {
                return new DatabaseConfigurationInfo(
                    DatabaseProviderType.Sqlite,
                    $"Data Source={sqliteFilePath}",
                    sqliteFilePath,
                    UsedSqliteFallback: true,
                    FallbackReason: probeError ?? "SQL Server connection string is not configured.");
            }
        }

        if (string.IsNullOrWhiteSpace(sqlServerConnectionString))
        {
            throw new InvalidOperationException("DefaultConnection is missing in configuration.");
        }

        return new DatabaseConfigurationInfo(
            DatabaseProviderType.SqlServer,
            sqlServerConnectionString,
            ExtractSqlServerTarget(sqlServerConnectionString),
            UsedSqliteFallback: false);
    }

    public static IServiceCollection AddDataAccessLayer(
        this IServiceCollection services,
        DatabaseConfigurationInfo dbInfo)
    {
        services.AddSingleton(dbInfo);

        services.AddDbContext<LibraryDbContext>(options =>
        {
            if (dbInfo.Provider == DatabaseProviderType.Sqlite)
            {
                options.UseSqlite(dbInfo.ConnectionString);
            }
            else
            {
                options.UseSqlServer(dbInfo.ConnectionString, sql =>
                    sql.MigrationsAssembly(typeof(LibraryDbContext).Assembly.FullName));
            }
        });

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<IBookRepository, BookRepository>();
        services.AddScoped<IBorrowingRepository, BorrowingRepository>();
        services.AddScoped<ISystemSettingRepository, SystemSettingRepository>();

        return services;
    }

    public static IServiceCollection AddDataAccessLayer(this IServiceCollection services, string connectionString)
    {
        var dbInfo = ResolveDatabaseConfiguration("SqlServer", connectionString, "library.db");
        return services.AddDataAccessLayer(dbInfo);
    }

    private static bool TryProbeSqlServer(string connectionString, out string? error)
    {
        try
        {
            var builder = new SqlConnectionStringBuilder(connectionString)
            {
                InitialCatalog = "master",
                ConnectTimeout = 3,
                Pooling = false
            };

            using var connection = new SqlConnection(builder.ConnectionString);
            connection.Open();
            error = null;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static string ExtractSqlServerTarget(string connectionString)
    {
        try
        {
            var builder = new SqlConnectionStringBuilder(connectionString);
            return $"{builder.DataSource} / {builder.InitialCatalog}";
        }
        catch
        {
            return "SQL Server";
        }
    }
}
