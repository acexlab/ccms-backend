/*
 * File: BankCustomerConfiguration.cs
 * Description: EF Core configuration mapping BankCustomer entity.
 * To Implement: Create indexes on AccountNumber, AadhaarNumber, PanNumber, and BankCode.
 */
// Crucial performance indexes for the background batch job matching algorithm
// Note for developer: These indexes are critical to avoid full-table scans during bulk batch execution.