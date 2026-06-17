using CCMS.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using CCMS.Domain.Entities;
using BCrypt.Net;

namespace CCMS.Infrastructure.Data;

public static class DatabaseSeeder
{
    public static async Task SeedAsync(AppDbContext context)
    {
        // Automatically create the database if it doesn't exist
        await context.Database.EnsureCreatedAsync();

        // Dynamically add the PanNumber column to the Defendants table if it doesn't exist yet
        try
        {
            await context.Database.ExecuteSqlRawAsync("ALTER TABLE `Defendants` ADD COLUMN `PanNumber` longtext NOT NULL;");
        }
        catch (Exception)
        {
            // Ignore if the column already exists
        }

        // 1. Seed Users (Role matches ENUM('Court', 'Bank') in DDL)
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
                }
            };

            await context.Users.AddRangeAsync(users);
            await context.SaveChangesAsync();
        }

        // Get court officer ID to associate with seeded cases
        var courtOfficer = await context.Users.FirstOrDefaultAsync(u => u.Username == "court_officer");
        int courtOfficerId = courtOfficer?.Id ?? 1;

        // 2. Seed Cases, Complainants, and Defendants
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

            var defendant = new Defendant
            {
                CaseId = sampleCase.Id,
                FullName = "John Smith",
                IdentityNumber = "1234-5678-9012",
                PanNumber = "ABCDE1234F",
                BankAccountNumber = "9876543210",
                BankName = "WEST"
            };
            await context.Defendants.AddAsync(defendant);

            await context.SaveChangesAsync();
        }

        // 3. Seed BankCustomers (Bank Data)
        if (!await context.BankCustomers.AnyAsync(c => c.AccountNumber == "9876543210"))
        {
            await context.BankCustomers.AddAsync(new BankCustomer
            {
                AccountNumber = "9876543210",
                AadhaarNumber = "1234-5678-9012",
                PANNumber = "ABCDE1234F",
                AccountHolderName = "John Smith",
                AccountStatus = AccountStatus.Active,
                CurrentBalance = 25000.00m
            });
        }

        if (!await context.BankCustomers.AnyAsync(c => c.AccountNumber == "111122223333"))
        {
            await context.BankCustomers.AddAsync(new BankCustomer
            {
                AccountNumber = "111122223333",
                AadhaarNumber = "123456789012",
                PANNumber = "ABCDE1234F",
                AccountHolderName = "Rajesh Kumar",
                AccountStatus = AccountStatus.Active,
                CurrentBalance = 150000.00m
            });
        }

        if (!await context.BankCustomers.AnyAsync(c => c.AccountNumber == "444455556666"))
        {
            await context.BankCustomers.AddAsync(new BankCustomer
            {
                AccountNumber = "444455556666",
                AadhaarNumber = "987654321098",
                PANNumber = "PQRSX5678Z",
                AccountHolderName = "Priya Sharma",
                AccountStatus = AccountStatus.Active,
                CurrentBalance = 75000.00m
            });
        }

        await context.SaveChangesAsync();

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
