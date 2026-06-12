/*
 * File: CaseNotFoundException.cs
 * Description: Exception thrown when a requested case entity is not found in the database.
 * To Implement: Handled by exception middleware to return a 404 HTTP status code.
 */

using System;

namespace ccms_backend.models;

public class CaseNotFoundException : Exception
{
    public CaseNotFoundException(int caseId)
        : base($"Case with ID {caseId} was not found.")
    {
    }
}
