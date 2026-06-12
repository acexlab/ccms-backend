/*
 * File: CaseStatus.cs
 * Description: Defines the possible states of a Court Case in the CCMS system.
 * To Implement: Keep in sync with frontend models and database configurations.
 */

namespace ccms_backend.models;

public enum CaseStatus
{
    Pending,
    AccountValidated,
    AccountNotFound,
    FreezeApplied,
    BalanceProvided
}
