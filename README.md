# Athenaeum — Library Management System (LMS)

A full-stack, role-based **Library Management System** built with **ASP.NET Core Web API (.NET 9)**, **Entity Framework Core**, **SQL Server** (with automatic portable **SQLite** fallback), and a responsive **HTML / CSS / JavaScript** frontend.

## Architecture

src/
├── LMS.WebApi/     # Presentation Layer — Thin REST controllers, JWT RBAC, & static HTML/CSS/JS UI
├── LMS.Business/   # Business Logic Layer — Services, ISBN validation, fine rules, & DTOs
└── LMS.Data/       # Data Access Layer — EF Core DbContext, Code-First Migrations, Repositories, & UnitOfWork
tests/
└── LMS.Tests/      # Automated xUnit Unit & Integration Test Suite (TC01–TC24 + Sprint 3 tests)
```

## Key Features

- **Role-Based Access Control (JWT + PBKDF2 Password Hashing):**
  - **Administrator:** Manage user accounts & roles, configure library policy (max borrow limit, loan duration, daily fine rate), and view the statistical dashboard with date-range filtering.
  - **Librarian:** Manage books (ISBN-10/13 validation, duplicate detection, active-borrow deletion guard), categories, library members, transactional borrow/return processing, and overdue monitoring.
  - **Library Member:** Search catalog by title, author, ISBN, or category with real-time copy availability, and view **only their own** borrowing history (strictly isolated from other members).
- **Transactional Inventory Integrity (NFR05):** Every borrow and return operation is wrapped in an explicit database transaction (`IUnitOfWork.BeginTransactionAsync`) with SQL Server `RowVersion` concurrency protection and check constraints.
- **Portable Database Support:** Runs against **SQL Server (`.\SQLEXPRESS`)** with EF Core Code-First Migrations by default, and includes an automatic **SQLite (`library.db`)** fallback mode for standalone Windows `.exe` deployments on machines without SQL Server.

## Prerequisites (Building from Source)

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- SQL Server Express (`.\SQLEXPRESS`) or LocalDB *(optional if running in SQLite mode)*

## Quick Start (Development)

```powershell
# Restore, build, and run the automated test suite (TC01–TC24 + Sprint 3)
dotnet test

# Run the web app (auto-migrates & seeds LibraryManagementDb on .\SQLEXPRESS)
dotnet run --project src/LMS.WebApi/LMS.WebApi.csproj --urls "http://localhost:5080"
```

Open **http://localhost:5080** in your browser.

### Force Portable SQLite Mode

To run with a local `library.db` file instead of SQL Server:

```powershell
dotnet run --project src/LMS.WebApi/LMS.WebApi.csproj --urls "http://localhost:5080" --DatabaseProvider=Sqlite
```

## Seeded Demo Accounts

| Role | Username | Password |
| :--- | :--- | :--- |
| **Administrator** | `admin` | `Admin@123` |
| **Librarian** | `librarian` | `Librarian@123` |
| **Library Member 1** (`MEM-001`) | `member1` | `Member@123` |
| **Library Member 2** (`MEM-002`) | `member2` | `Member@123` |

## Standalone Windows `.exe` Distribution

Build a self-contained single-file Windows x64 executable (bundling the .NET runtime, SQLite native engine, and `wwwroot` frontend):

```powershell
dotnet publish src/LMS.WebApi/LMS.WebApi.csproj -c Release -r win-x64 --self-contained true -o publish/win-x64
```

Double-click or run `publish\win-x64\AthenaeumLMS.exe`, then open **http://localhost:5080**.
In Release/Production mode (`DatabaseProvider: "Auto"`), the executable connects to `.\SQLEXPRESS` if available and otherwise automatically creates and seeds a portable `library.db` right next to `AthenaeumLMS.exe`.
