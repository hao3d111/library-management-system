using LMS.Data.Context;
using LMS.Data.Entities;
using LMS.Data.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace LMS.Data.Seed;

public static class DatabaseSeeder
{
    public static async Task SeedAsync(LibraryDbContext context, CancellationToken ct = default)
    {
        var hasher = new PasswordHasher<User>();

        if (!await context.SystemSettings.AnyAsync(ct))
        {
            context.SystemSettings.Add(new SystemSetting
            {
                MaxBorrowLimit = 5,
                LoanDurationDays = 14,
                DailyFineAmount = 1.00m,
                UpdatedAt = DateTime.UtcNow
            });
            await context.SaveChangesAsync(ct);
        }

        if (!await context.Users.AnyAsync(ct))
        {
            var admin = new User
            {
                Username = "admin",
                FullName = "System Administrator",
                Email = "admin@lms.local",
                Phone = "555-0100",
                Role = UserRole.Administrator,
                IsActive = true,
                CreatedAt = DateTime.UtcNow.AddDays(-60)
            };
            admin.PasswordHash = hasher.HashPassword(admin, "Admin@123");

            var librarian = new User
            {
                Username = "librarian",
                FullName = "Clara Vance",
                Email = "librarian@lms.local",
                Phone = "555-0101",
                Role = UserRole.Librarian,
                IsActive = true,
                CreatedAt = DateTime.UtcNow.AddDays(-45)
            };
            librarian.PasswordHash = hasher.HashPassword(librarian, "Librarian@123");

            var member1 = new User
            {
                Username = "member1",
                FullName = "Alice Johnson",
                Email = "alice.johnson@lms.local",
                Phone = "555-0201",
                MembershipCode = "MEM-001",
                Role = UserRole.LibraryMember,
                IsActive = true,
                CreatedAt = DateTime.UtcNow.AddDays(-30)
            };
            member1.PasswordHash = hasher.HashPassword(member1, "Member@123");

            var member2 = new User
            {
                Username = "member2",
                FullName = "Bob Smith",
                Email = "bob.smith@lms.local",
                Phone = "555-0202",
                MembershipCode = "MEM-002",
                Role = UserRole.LibraryMember,
                IsActive = true,
                CreatedAt = DateTime.UtcNow.AddDays(-25)
            };
            member2.PasswordHash = hasher.HashPassword(member2, "Member@123");

            context.Users.AddRange(admin, librarian, member1, member2);
            await context.SaveChangesAsync(ct);
        }

        if (!await context.Categories.AnyAsync(ct))
        {
            var categories = new List<Category>
            {
                new() { Name = "Software Engineering", Description = "Architecture, clean code, agile practices, and software craftsmanship." },
                new() { Name = "Computer Science", Description = "Algorithms, operating systems, databases, and distributed systems." },
                new() { Name = "Fiction", Description = "Classic and contemporary literary fiction and novels." },
                new() { Name = "History", Description = "World history, biographies, and historical essays." },
                new() { Name = "Science & Mathematics", Description = "Physics, astronomy, mathematics, and natural sciences." }
            };
            context.Categories.AddRange(categories);
            await context.SaveChangesAsync(ct);
        }

        if (!await context.Books.AnyAsync(ct))
        {
            var categories = await context.Categories.ToDictionaryAsync(c => c.Name, c => c.Id, ct);

            var books = new List<Book>
            {
                new()
                {
                    Title = "Clean Code",
                    Author = "Robert C. Martin",
                    ISBN = "978-0132350884",
                    CategoryId = categories["Software Engineering"],
                    TotalQuantity = 5,
                    AvailableQuantity = 4,
                    Description = "A handbook of agile software craftsmanship."
                },
                new()
                {
                    Title = "The Pragmatic Programmer",
                    Author = "David Thomas & Andrew Hunt",
                    ISBN = "978-0135957059",
                    CategoryId = categories["Software Engineering"],
                    TotalQuantity = 4,
                    AvailableQuantity = 3,
                    Description = "Your journey to mastery, 20th Anniversary Edition."
                },
                new()
                {
                    Title = "Designing Data-Intensive Applications",
                    Author = "Martin Kleppmann",
                    ISBN = "978-1449373320",
                    CategoryId = categories["Computer Science"],
                    TotalQuantity = 3,
                    AvailableQuantity = 3,
                    Description = "The big ideas behind reliable, scalable, and maintainable systems."
                },
                new()
                {
                    Title = "Introduction to Algorithms",
                    Author = "Thomas H. Cormen",
                    ISBN = "978-0262046305",
                    CategoryId = categories["Computer Science"],
                    TotalQuantity = 2,
                    AvailableQuantity = 2,
                    Description = "Comprehensive guide to modern algorithms and data structures."
                },
                new()
                {
                    Title = "Dune",
                    Author = "Frank Herbert",
                    ISBN = "978-0441172719",
                    CategoryId = categories["Fiction"],
                    TotalQuantity = 4,
                    AvailableQuantity = 4,
                    Description = "Epic science fiction novel set on the desert planet Arrakis."
                },
                new()
                {
                    Title = "Sapiens: A Brief History of Humankind",
                    Author = "Yuval Noah Harari",
                    ISBN = "978-0062316097",
                    CategoryId = categories["History"],
                    TotalQuantity = 3,
                    AvailableQuantity = 3,
                    Description = "Explores the history of Homo sapiens from the Stone Age to the modern era."
                },
                new()
                {
                    Title = "A Brief History of Time",
                    Author = "Stephen Hawking",
                    ISBN = "978-0553380163",
                    CategoryId = categories["Science & Mathematics"],
                    TotalQuantity = 2,
                    AvailableQuantity = 2,
                    Description = "Landmark volume in science writing exploring cosmology and black holes."
                },
                new()
                {
                    Title = "Refactoring",
                    Author = "Martin Fowler",
                    ISBN = "978-0134757599",
                    CategoryId = categories["Software Engineering"],
                    TotalQuantity = 1,
                    AvailableQuantity = 1,
                    Description = "Improving the design of existing code."
                }
            };

            context.Books.AddRange(books);
            await context.SaveChangesAsync(ct);
        }

        if (!await context.BorrowingTransactions.AnyAsync(ct))
        {
            var librarian = await context.Users.FirstAsync(u => u.Username == "librarian", ct);
            var member1 = await context.Users.FirstAsync(u => u.Username == "member1", ct);
            var member2 = await context.Users.FirstAsync(u => u.Username == "member2", ct);
            var cleanCode = await context.Books.FirstAsync(b => b.ISBN == "978-0132350884", ct);
            var pragmatic = await context.Books.FirstAsync(b => b.ISBN == "978-0135957059", ct);
            var dune = await context.Books.FirstAsync(b => b.ISBN == "978-0441172719", ct);

            var now = DateTime.UtcNow;
            var transactions = new List<BorrowingTransaction>
            {
                // Active on-time borrowing for member1 (matches Clean Code AvailableQuantity = 4/5)
                new()
                {
                    MemberId = member1.Id,
                    BookId = cleanCode.Id,
                    ProcessedByUserId = librarian.Id,
                    BorrowDate = now.AddDays(-3),
                    DueDate = now.AddDays(11),
                    Status = BorrowingStatus.Borrowed,
                    FineAmount = 0m
                },
                // Past-due active borrowing for member2 (matches Pragmatic Programmer AvailableQuantity = 3/4)
                new()
                {
                    MemberId = member2.Id,
                    BookId = pragmatic.Id,
                    ProcessedByUserId = librarian.Id,
                    BorrowDate = now.AddDays(-20),
                    DueDate = now.AddDays(-6),
                    Status = BorrowingStatus.Overdue,
                    FineAmount = 0m
                },
                // Historical returned borrowing for member1
                new()
                {
                    MemberId = member1.Id,
                    BookId = dune.Id,
                    ProcessedByUserId = librarian.Id,
                    BorrowDate = now.AddDays(-25),
                    DueDate = now.AddDays(-11),
                    ReturnDate = now.AddDays(-13),
                    Status = BorrowingStatus.Returned,
                    FineAmount = 0m
                }
            };

            context.BorrowingTransactions.AddRange(transactions);
            await context.SaveChangesAsync(ct);
        }
    }
}
