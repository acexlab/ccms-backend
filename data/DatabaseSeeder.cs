/*
 * File: DatabaseSeeder.cs
 * Description: Seeds database with initial metadata users and customers for testing purposes.
 * To Implement: Run only in Development environment during startup.
 */

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ccms_backend.models;

namespace ccms_backend.data;

public static class DatabaseSeeder
{
    public static async Task SeedAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Ensure database is created
        await context.Database.EnsureCreatedAsync();

        // 1. Seed Users
        if (!context.Users.Any())
        {
            var courtUser = new User
            {
                Username = "court_user",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Court@123"),
                Role = UserRole.CourtOfficer,
                BankCode = null
            };

            var bankUser = new User
            {
                Username = "bank_user",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Bank@123"),
                Role = UserRole.BankOfficer,
                BankCode = "BANK001"
            };

            await context.Users.AddRangeAsync(courtUser, bankUser);
            await context.SaveChangesAsync();
        }

        // 2. Seed Bank Customers (15 records)
        if (!context.BankCustomers.Any())
        {
            var customers = Enumerable.Range(1, 15).Select(i => new BankCustomer
            {
                AccountNumber = $"ACC{i:D8}",
                AadhaarNumber = $"1111222233{i:D2}",
                PanNumber = $"ABCDE12{i:D2}F",
                Balance = 1000.00m * i,
                AccountStatus = i % 5 == 0 ? "Closed" : "Active",
                BankCode = "BANK001"
            }).ToList();

            // Give some specific test data to match explicitly in tests
            // Customer 1 Aadhaar = 111122223301, AccountNumber = ACC00000001, PAN = ABCDE1201F
            await context.BankCustomers.AddRangeAsync(customers);
            await context.SaveChangesAsync();
        }
    }
}
