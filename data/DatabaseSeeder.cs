using Microsoft.EntityFrameworkCore;
using ccms_backend.models;

namespace ccms_backend.data;

public static class DatabaseSeeder
{
    public static async Task SeedAsync(AppDbContext context)
    {
        // Automatically create the database if it doesn't exist
        await context.Database.EnsureCreatedAsync();

        // 1. Seed Banks
        if (!await context.Banks.AnyAsync())
        {
            var banks = new List<Bank>
            {
                new Bank { Name = "State Bank of India", Code = "SBI" },
                new Bank { Name = "HDFC Bank", Code = "HDFC" }
            };
            await context.Banks.AddRangeAsync(banks);
            await context.SaveChangesAsync();
        }

        // 2. Seed Users (Role matches ENUM('Court', 'Bank') in DDL)
        if (!await context.Users.AnyAsync())
        {
            var users = new List<User>
            {
                new User
                {
                    Username = "court_officer",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("court_officer_pwd"),
                    Role = UserRole.Court
                },
                new User
                {
                    Username = "bank_officer",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("bank_officer_pwd"),
                    Role = UserRole.Bank
                },
                new User
                {
                    Username = "court.user",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password@123"),
                    Role = UserRole.Court
                },
                new User
                {
                    Username = "bank.user",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password@123"),
                    Role = UserRole.Bank,
                    BankCode = "SBI"
                },
                new User
                {
                    Username = "hdfc.user",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password@123"),
                    Role = UserRole.Bank,
                    BankCode = "HDFC"
                }
            };

            await context.Users.AddRangeAsync(users);
            await context.SaveChangesAsync();
        }

        // Get court officer ID to associate with seeded cases
        var courtOfficer = await context.Users.FirstOrDefaultAsync(u => u.Username == "court_officer");
        int courtOfficerId = courtOfficer?.Id ?? 1;

        // 3. Seed Cases, Complainants, and Defendants
        if (!await context.Cases.AnyAsync())
        {
            var sampleCase = new Case
            {
                CaseNumber = "CCMS-20260615-0001",
                OrderType = OrderType.FreezeAccount,
                Status = CaseStatus.Pending,
                FreezeAmount = 15000.00m,
                CreatedByUserId = courtOfficerId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await context.Cases.AddAsync(sampleCase);
            await context.SaveChangesAsync();

            // Seed associated Complainant
            var complainant = new Complainant
            {
                CaseId = sampleCase.Id,
                FullName = "State Legal Authority",
                IdentityNumber = "AUTH-ID-8844"
            };
            await context.Complainants.AddAsync(complainant);

            // Seed associated Defendant
            var defendant = new Defendant
            {
                CaseId = sampleCase.Id,
                FullName = "John Smith",
                IdentityNumber = "Aadhaar: 1234-5678-9012, PAN: ABCDE1234F",
                BankAccountNumber = "9876543210",
                BankName = "SBI" // changed to match seeded bank
            };
            await context.Defendants.AddAsync(defendant);

            await context.SaveChangesAsync();
        }

        // 4. Seed BankCustomers (Bank Data)
        if (!await context.BankCustomers.AnyAsync())
        {
            var sampleCustomer = new BankCustomer
            {
                AccountNumber = "9876543210",
                AadhaarNumber = "1234-5678-9012",
                PANNumber = "ABCDE1234F",
                AccountHolderName = "John Smith",
                AccountStatus = AccountStatus.Active,
                CurrentBalance = 25000.00m,
                BankCode = "SBI"
            };

            await context.BankCustomers.AddAsync(sampleCustomer);
            await context.SaveChangesAsync();
        }

        // 4. Seed BatchJobLogs
        if (!await context.BatchJobLogs.AnyAsync())
        {
            var logs = new List<BatchJobLog>
            {
                new BatchJobLog
                {
                    RunId = "BATCH-20231027-01",
                    TriggeredBy = TriggeredBy.Scheduled,
                    StartTime = new DateTime(2023, 10, 27, 22, 0, 4, DateTimeKind.Utc),
                    EndTime = new DateTime(2023, 10, 28, 0, 14, 12, DateTimeKind.Utc),
                    CasesProcessed = 12482,
                    AccountsMatched = 11904,
                    AccountsNotFound = 578,
                    DurationSeconds = 8048,
                    Status = BatchJobStatus.Success
                },
                new BatchJobLog
                {
                    RunId = "BATCH-20231026-01",
                    TriggeredBy = TriggeredBy.Scheduled,
                    StartTime = new DateTime(2023, 10, 26, 22, 0, 1, DateTimeKind.Utc),
                    EndTime = new DateTime(2023, 10, 27, 0, 8, 55, DateTimeKind.Utc),
                    CasesProcessed = 12100,
                    AccountsMatched = 12100,
                    AccountsNotFound = 0,
                    DurationSeconds = 7734,
                    Status = BatchJobStatus.Success
                },
                new BatchJobLog
                {
                    RunId = "BATCH-20231025-01",
                    TriggeredBy = TriggeredBy.Scheduled,
                    StartTime = new DateTime(2023, 10, 25, 22, 0, 5, DateTimeKind.Utc),
                    EndTime = new DateTime(2023, 10, 25, 23, 15, 30, DateTimeKind.Utc),
                    CasesProcessed = 10500,
                    AccountsMatched = 9500,
                    AccountsNotFound = 1000,
                    DurationSeconds = 4525,
                    Status = BatchJobStatus.Success
                },
                new BatchJobLog
                {
                    RunId = "BATCH-20231024-01",
                    TriggeredBy = TriggeredBy.Scheduled,
                    StartTime = new DateTime(2023, 10, 24, 22, 0, 2, DateTimeKind.Utc),
                    EndTime = new DateTime(2023, 10, 25, 0, 5, 12, DateTimeKind.Utc),
                    CasesProcessed = 11000,
                    AccountsMatched = 10000,
                    AccountsNotFound = 1000,
                    DurationSeconds = 7510,
                    Status = BatchJobStatus.Success
                }
            };
            await context.BatchJobLogs.AddRangeAsync(logs);
            await context.SaveChangesAsync();
        }
    }
}