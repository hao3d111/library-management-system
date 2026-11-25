using LMS.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace LMS.Data.Context;

public class LibraryDbContext : DbContext
{
    public LibraryDbContext(DbContextOptions<LibraryDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Book> Books => Set<Book>();
    public DbSet<BorrowingTransaction> BorrowingTransactions => Set<BorrowingTransaction>();
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("Users");
            entity.HasKey(u => u.Id);
            entity.Property(u => u.Username).IsRequired().HasMaxLength(50);
            entity.Property(u => u.PasswordHash).IsRequired().HasMaxLength(500);
            entity.Property(u => u.FullName).IsRequired().HasMaxLength(120);
            entity.Property(u => u.Email).IsRequired().HasMaxLength(150);
            entity.Property(u => u.Phone).HasMaxLength(30);
            entity.Property(u => u.MembershipCode).HasMaxLength(30);
            entity.Property(u => u.Role).HasConversion<string>().HasMaxLength(30).IsRequired();
            entity.Property(u => u.IsActive).HasDefaultValue(true);

            entity.HasIndex(u => u.Username).IsUnique();
            entity.HasIndex(u => u.Email).IsUnique();
            entity.HasIndex(u => u.MembershipCode)
                .IsUnique()
                .HasFilter("[MembershipCode] IS NOT NULL");
        });

        modelBuilder.Entity<Category>(entity =>
        {
            entity.ToTable("Categories");
            entity.HasKey(c => c.Id);
            entity.Property(c => c.Name).IsRequired().HasMaxLength(100);
            entity.Property(c => c.Description).HasMaxLength(300);

            entity.HasIndex(c => c.Name).IsUnique();
        });

        modelBuilder.Entity<Book>(entity =>
        {
            entity.ToTable("Books", t =>
            {
                t.HasCheckConstraint("CK_Books_TotalQuantity", "[TotalQuantity] >= 0");
                t.HasCheckConstraint("CK_Books_AvailableQuantity", "[AvailableQuantity] >= 0 AND [AvailableQuantity] <= [TotalQuantity]");
            });
            entity.HasKey(b => b.Id);
            entity.Property(b => b.Title).IsRequired().HasMaxLength(200);
            entity.Property(b => b.Author).IsRequired().HasMaxLength(150);
            entity.Property(b => b.ISBN).IsRequired().HasMaxLength(20);
            entity.Property(b => b.Description).HasMaxLength(1000);
            if (Database.IsSqlite())
            {
                entity.Property(b => b.RowVersion)
                    .IsRequired(false)
                    .ValueGeneratedNever()
                    .IsConcurrencyToken(false);
            }
            else
            {
                entity.Property(b => b.RowVersion).IsRowVersion();
            }

            entity.HasIndex(b => b.ISBN).IsUnique();
            entity.HasIndex(b => b.Title);
            entity.HasIndex(b => b.Author);

            entity.HasOne(b => b.Category)
                .WithMany(c => c.Books)
                .HasForeignKey(b => b.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<BorrowingTransaction>(entity =>
        {
            entity.ToTable("BorrowingTransactions");
            entity.HasKey(t => t.Id);
            entity.Property(t => t.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.Property(t => t.FineAmount).HasColumnType("decimal(10,2)").HasDefaultValue(0.00m);

            entity.HasIndex(t => t.MemberId);
            entity.HasIndex(t => t.BookId);
            entity.HasIndex(t => new { t.Status, t.DueDate });

            entity.HasOne(t => t.Member)
                .WithMany(u => u.Borrowings)
                .HasForeignKey(t => t.MemberId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(t => t.Book)
                .WithMany(b => b.BorrowingTransactions)
                .HasForeignKey(t => t.BookId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(t => t.ProcessedByUser)
                .WithMany(u => u.ProcessedBorrowings)
                .HasForeignKey(t => t.ProcessedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<SystemSetting>(entity =>
        {
            entity.ToTable("SystemSettings");
            entity.HasKey(s => s.Id);
            entity.Property(s => s.DailyFineAmount).HasColumnType("decimal(10,2)");
        });
    }
}
