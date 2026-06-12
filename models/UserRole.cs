/*
 * File: UserRole.cs
 * Description: Defines client authentication roles (CourtOfficer, BankOfficer).
 * To Implement: Role authorization mappings in ASP.NET Core controllers and frontend route guards.
 */

namespace ccms_backend.models;

public enum UserRole
{
    CourtOfficer,
    BankOfficer
}
