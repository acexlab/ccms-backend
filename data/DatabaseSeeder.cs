using Microsoft.EntityFrameworkCore;
using ccms_backend.models;

namespace ccms_backend.data;

public static class DatabaseSeeder
{
    public static async Task SeedAsync(AppDbContext context)
    {
        // Automatically create the database if it doesn't exist
        await context.Database.EnsureCreatedAsync();

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

            // Seed associated Defendant
            var defendant = new Defendant
            {
                CaseId = sampleCase.Id,
                FullName = "John Smith",
                IdentityNumber = "Aadhaar: 1234-5678-9012, PAN: ABCDE1234F",
                BankAccountNumber = "9876543210",
                BankName = "WEST"
            };
            await context.Defendants.AddAsync(defendant);

            await context.SaveChangesAsync();
        }

        // 3. Seed BankCustomers (Bank Data)
        if (!await context.BankCustomers.AnyAsync())
        {
            var sampleCustomer = new BankCustomer
            {
                AccountNumber = "9876543210",
                AadhaarNumber = "1234-5678-9012",
                PANNumber = "ABCDE1234F",
                AccountHolderName = "John Smith",
                AccountStatus = AccountStatus.Active,
                CurrentBalance = 25000.00m
            };

            await context.BankCustomers.AddAsync(sampleCustomer);
            await context.SaveChangesAsync();
        }
    }
}