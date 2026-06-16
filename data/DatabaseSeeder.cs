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

        // 2. Seed Users
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

        // Get user IDs for associating seeded data
        var courtUser = await context.Users.FirstOrDefaultAsync(u => u.Username == "court.user");
        int courtUserId = courtUser?.Id ?? 1;

        var bankUser = await context.Users.FirstOrDefaultAsync(u => u.Username == "bank.user");
        int bankUserId = bankUser?.Id ?? 2;

        var hdfcUser = await context.Users.FirstOrDefaultAsync(u => u.Username == "hdfc.user");
        int hdfcUserId = hdfcUser?.Id ?? 3;

        // 3. Seed BankCustomers (Mock Core Banking Data)
        if (!await context.BankCustomers.AnyAsync())
        {
            var customers = new List<BankCustomer>
            {
                new BankCustomer
                {
                    AccountNumber = "9876543210",
                    AadhaarNumber = "1234-5678-9012",
                    PANNumber = "ABCDE1234F",
                    AccountHolderName = "John Smith",
                    AccountStatus = AccountStatus.Active,
                    TotalBalance = 25000.00m,
                    AvailableBalance = 25000.00m,
                    FrozenAmount = 0.00m,
                    BankCode = "SBI"
                },
                new BankCustomer
                {
                    AccountNumber = "111122223333",
                    AadhaarNumber = "123456789012",
                    PANNumber = "ABCDE1234F",
                    AccountHolderName = "Rajesh Kumar",
                    AccountStatus = AccountStatus.Active,
                    TotalBalance = 150000.00m,
                    AvailableBalance = 150000.00m,
                    FrozenAmount = 0.00m,
                    BankCode = "SBI"
                },
                new BankCustomer
                {
                    AccountNumber = "444455556666",
                    AadhaarNumber = "987654321098",
                    PANNumber = "PQRSX5678Z",
                    AccountHolderName = "Priya Sharma",
                    AccountStatus = AccountStatus.Active,
                    TotalBalance = 75000.00m,
                    AvailableBalance = 75000.00m,
                    FrozenAmount = 0.00m,
                    BankCode = "HDFC"
                },
                new BankCustomer
                {
                    AccountNumber = "777788889999",
                    AadhaarNumber = "222233334444",
                    PANNumber = "LMNOP4321K",
                    AccountHolderName = "Amit Verma",
                    AccountStatus = AccountStatus.Frozen,
                    TotalBalance = 20000.00m,
                    AvailableBalance = 0.00m,
                    FrozenAmount = 20000.00m,
                    BankCode = "SBI"
                }
            };

            await context.BankCustomers.AddRangeAsync(customers);
            await context.SaveChangesAsync();
        }

        // 4. Seed Cases, Complainants, Defendants, Validation Results, and Responses
        if (!await context.Cases.AnyAsync())
        {
            // Helper function to add cases and seed corresponding entities
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

            // Case 0: Standard Legacy Pending Case (John Smith)
            await AddTestCase("CCMS-20260615-0001", OrderType.FreezeAccount, 15000.00m, CaseStatus.Pending,
                "State Legal Authority", "AUTH-ID-8844", "John Smith", "Aadhaar: 1234-5678-9012, PAN: ABCDE1234F", "9876543210", "SBI");

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
                BalanceReported = 75000m,
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

            // Case I: Balance Enquiry, Account Validated (HDFC)
            var caseI = await AddTestCase("CCMS-TEST-0009", OrderType.BalanceEnquiry, null, CaseStatus.AccountValidated,
                "Central Police", "POL999", "Priya Sharma", "987654321098", "444455556666", "HDFC");
            var valI = new CaseValidationResult
            {
                CaseId = caseI.Id,
                MatchedAccountNumber = "444455556666",
                AccountHolderName = "Priya Sharma",
                AccountStatus = "Active",
                CurrentBalance = 75000m,
                MatchedOn = MatchedOn.AccountNumber,
                ValidatedAt = DateTime.UtcNow
            };
            await context.CaseValidationResults.AddAsync(valI);

            await context.SaveChangesAsync();
        }

        // 5. Seed BatchJobLogs
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