# Library Management System (LMS) — Persistent Project Rules

Follow these architectural, security, data-integrity, and workflow rules for every change in this repository:

1. **Strict Three-Layer Architecture**
   - Maintain strict separation of concerns across **Presentation (`LMS.WebApi`) -> Business Logic / Services (`LMS.Business`) -> Data Access / Repositories & DbContext (`LMS.Data`)**.
   - Never place business logic, validation workflows, fine calculations, or direct `DbContext` queries inside API controllers. Controllers must remain thin HTTP adapters that delegate to service interfaces.

2. **Transactional Inventory Consistency (NFR05)**
   - Wrap every **borrow** and **return** operation in an explicit database transaction (`IUnitOfWork.BeginTransactionAsync` / `CommitAsync` / `RollbackAsync`) to guarantee atomic inventory consistency between `Book.AvailableQuantity` and `BorrowingTransaction` records.

3. **Security, Password Hashing & Role-Based Access Control (NFR02, NFR09)**
   - Hash all user passwords securely (using ASP.NET Core Identity `PasswordHasher` / PBKDF2-HMAC-SHA256; never store plaintext or weak hashes).
   - Enforce JWT authentication and strict role-based authorization (`Administrator`, `Librarian`, `LibraryMember`) on all protected API endpoints and UI routes.
   - Enforce object-level authorization so a `LibraryMember` can **never** view or access another member's borrowing history or private data (`403 Forbidden`).

4. **Plan-First Governance**
   - Always generate an **Implementation Plan Artifact** for user approval before modifying **5 or more files**.

5. **Regression Verification Gate**
   - After any feature addition or modification, run the full automated test suite (`dotnet test`) to verify there are no regressions across **TC01–TC24** (plus any sprint-specific tests).
