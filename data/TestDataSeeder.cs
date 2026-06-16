using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ccms_backend.models;

namespace ccms_backend.data;

public static class TestDataSeeder
{
    public static async Task SeedAsync(AppDbContext context)
    {
        // 1. Recreate the database schema
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();

        // 2. Seed Banks
        var sbi = new Bank { Name = "State Bank of India", Code = "SBI" };
        var hdfc = new Bank { Name = "HDFC Bank", Code = "HDFC" };
        await context.Banks.AddRangeAsync(sbi, hdfc);
        await context.SaveChangesAsync();

        // 3. Seed Users with hashed passwords
        var courtUser = new User
        {
            Username = "court.user",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password@123"),
            Role = UserRole.Court
        };

        var bankUser = new User
        {
            Username = "bank.user",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password@123"),
            Role = UserRole.Bank,
            BankCode = "SBI"
        };

        var hdfcUser = new User
        {
            Username = "hdfc.user",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password@123"),
            Role = UserRole.Bank,
            BankCode = "HDFC"
        };

        await context.Users.AddRangeAsync(courtUser, bankUser, hdfcUser);
        await context.SaveChangesAsync();

        int courtUserId = courtUser.Id;
        int bankUserId = bankUser.Id;
        int hdfcUserId = hdfcUser.Id;

        // 4. Seed Bank Customers
        var cust1 = new BankCustomer
        {
            AccountHolderName = "Rajesh Kumar",
            AccountNumber = "111122223333",
            AadhaarNumber = "123456789012",
            PANNumber = "ABCDE1234F",
            BankCode = "SBI",
            CurrentBalance = 150000m,
            AccountStatus = AccountStatus.Active
        };

        var cust2 = new BankCustomer
        {
            AccountHolderName = "Priya Sharma",
            AccountNumber = "444455556666",
            AadhaarNumber = "987654321098",
            PANNumber = "PQRSX5678Z",
            BankCode = "HDFC",
            CurrentBalance = 75000m,
            AccountStatus = AccountStatus.Active
        };

        var cust3 = new BankCustomer
        {
            AccountHolderName = "Amit Verma",
            AccountNumber = "777788889999",
            AadhaarNumber = "222233334444",
            PANNumber = "LMNOP4321K",
            BankCode = "SBI",
            CurrentBalance = 20000m,
            AccountStatus = AccountStatus.Frozen
        };

        await context.BankCustomers.AddRangeAsync(cust1, cust2, cust3);
        await context.SaveChangesAsync();

        // Helper function to create Cases
        async Task<Case> AddTestCase(string caseNumber, OrderType orderType, decimal? freezeAmount, CaseStatus status, string compName, string compId, string defName, string defId, string defAcc, string defBank)
        {
            var c = new Case
            {
                CaseNumber = caseNumber,
                OrderType = orderType,
                FreezeAmount = freezeAmount,
                Status = status,
                CreatedByUserId = courtUserId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            await context.Cases.AddAsync(c);
            await context.SaveChangesAsync();

            var comp = new Complainant
            {
                CaseId = c.Id,
                FullName = compName,
                IdentityNumber = compId
            };
            var def = new Defendant
            {
                CaseId = c.Id,
                FullName = defName,
                IdentityNumber = defId,
                BankAccountNumber = defAcc,
                BankName = defBank
            };
            await context.Complainants.AddAsync(comp);
            await context.Defendants.AddAsync(def);
            await context.SaveChangesAsync();

            return c;
        }

        // Case A: Freeze Account, match by Account Number (SBI)
        await AddTestCase("CCMS-TEST-0001", OrderType.FreezeAccount, 10000m, CaseStatus.Pending,
            "State Tax Dept", "TAX12345", "Rajesh Kumar", "123456789012", "111122223333", "SBI");

        // Case B: Balance Enquiry, match by Aadhaar (HDFC)
        await AddTestCase("CCMS-TEST-0002", OrderType.BalanceEnquiry, null, CaseStatus.Pending,
            "State Tax Dept", "TAX12345", "Priya Sharma", "987654321098", "INVALID", "HDFC");

        // Case C: Freeze Account, match by PAN (SBI)
        await AddTestCase("CCMS-TEST-0003", OrderType.FreezeAccount, 5000m, CaseStatus.Pending,
            "State Tax Dept", "TAX12345", "Amit Verma", "LMNOP4321K", "INVALID", "SBI");

        // Case D: Balance Enquiry, no match expected (SBI)
        await AddTestCase("CCMS-TEST-0004", OrderType.BalanceEnquiry, null, CaseStatus.Pending,
            "State Tax Dept", "TAX12345", "Unknown Person", "000000000000", "999999999999", "SBI");

        // Case E: Freeze Account, Account Validated
        var caseE = await AddTestCase("CCMS-TEST-0005", OrderType.FreezeAccount, 12000m, CaseStatus.AccountValidated,
            "Central Police", "POL999", "Rajesh Kumar", "123456789012", "111122223333", "SBI");
        var valE = new CaseValidationResult
        {
            CaseId = caseE.Id,
            MatchedAccountNumber = "111122223333",
            AccountHolderName = "Rajesh Kumar",
            AccountStatus = "Active",
            CurrentBalance = 150000m,
            MatchedOn = MatchedOn.AccountNumber,
            ValidatedAt = DateTime.UtcNow
        };
        await context.CaseValidationResults.AddAsync(valE);

        // Case F: Freeze Account, Completed Freeze (Freeze Applied)
        var caseF = await AddTestCase("CCMS-TEST-0006", OrderType.FreezeAccount, 50000m, CaseStatus.FreezeApplied,
            "Central Police", "POL999", "Rajesh Kumar", "123456789012", "111122223333", "SBI");
        var valF = new CaseValidationResult
        {
            CaseId = caseF.Id,
            MatchedAccountNumber = "111122223333",
            AccountHolderName = "Rajesh Kumar",
            AccountStatus = "Active",
            CurrentBalance = 150000m,
            MatchedOn = MatchedOn.AccountNumber,
            ValidatedAt = DateTime.UtcNow
        };
        var respF = new CaseResponse
        {
            CaseId = caseF.Id,
            RespondedByUserId = bankUserId,
            ResponseType = ResponseType.FreezeApplied,
            FreezeAmountApplied = 50000m,
            Remarks = "Freeze applied successfully",
            SubmittedAt = DateTime.UtcNow
        };
        await context.CaseValidationResults.AddAsync(valF);
        await context.CaseResponses.AddAsync(respF);

        // Case G: Balance Enquiry, Completed Balance (Balance Provided)
        var caseG = await AddTestCase("CCMS-TEST-0007", OrderType.BalanceEnquiry, null, CaseStatus.BalanceProvided,
            "Central Police", "POL999", "Priya Sharma", "987654321098", "444455556666", "HDFC");
        var valG = new CaseValidationResult
        {
            CaseId = caseG.Id,
            MatchedAccountNumber = "444455556666",
            AccountHolderName = "Priya Sharma",
            AccountStatus = "Active",
            CurrentBalance = 75000m,
            MatchedOn = MatchedOn.AccountNumber,
            ValidatedAt = DateTime.UtcNow
        };
        var respG = new CaseResponse
        {
            CaseId = caseG.Id,
            RespondedByUserId = hdfcUserId,
            ResponseType = ResponseType.BalanceProvided,
            BalanceReported = 125000m,
            Remarks = "Balance confirmed",
            SubmittedAt = DateTime.UtcNow
        };
        await context.CaseValidationResults.AddAsync(valG);
        await context.CaseResponses.AddAsync(respG);

        // Case H: Balance Enquiry, Account Not Found
        var caseH = await AddTestCase("CCMS-TEST-0008", OrderType.BalanceEnquiry, null, CaseStatus.AccountNotFound,
            "Central Police", "POL999", "Unknown Person", "000000000000", "999999999999", "SBI");
        var respH = new CaseResponse
        {
            CaseId = caseH.Id,
            RespondedByUserId = bankUserId,
            ResponseType = ResponseType.AccountNotFound,
            Remarks = "No matching account found in bank records",
            SubmittedAt = DateTime.UtcNow
        };
        await context.CaseResponses.AddAsync(respH);

        await context.SaveChangesAsync();
    }
}
