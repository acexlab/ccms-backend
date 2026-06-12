/*
 * File: UnauthorisedActionException.cs
 * Description: Exception thrown when an unauthorized user or role attempts to access or modify resources.
 * To Implement: Handled by exception middleware to return a 403 Forbidden HTTP status code.
 */

using System;

namespace ccms_backend.models;

public class UnauthorisedActionException : Exception
{
    public UnauthorisedActionException(string message) : base(message)
    {
    }
}
