/*
 * File: CaseAlreadyRespondedException.cs
 * Description: Exception thrown when trying to respond to or modify a case that has already received a response.
 * To Implement: Handled by exception middleware to return a 409 Conflict HTTP status code.
 */

using System;

namespace ccms_backend.models;

public class CaseAlreadyRespondedException : Exception
{
    public CaseAlreadyRespondedException(string caseNumber)
        : base($"Case {caseNumber} has already received a response and cannot be modified.")
    {
    }
}
