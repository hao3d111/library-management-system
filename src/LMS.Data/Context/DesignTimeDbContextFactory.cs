using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace LMS.Data.Context;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<LibraryDbContext>
{
    public LibraryDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<LibraryDbContext>();
        var connectionString = Environment.GetEnvironmentVariable("LMS_CONNECTION_STRING")
            ?? "Server=.\\SQLEXPRESS;Database=LibraryManagementDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True;";
        optionsBuilder.UseSqlServer(connectionString, sql =>
            sql.MigrationsAssembly(typeof(LibraryDbContext).Assembly.FullName));
        return new LibraryDbContext(optionsBuilder.Options);
    }
}
