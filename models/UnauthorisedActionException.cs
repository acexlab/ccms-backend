using System;

namespace ccms_backend.models;

public class UnauthorisedActionException : Exception
{
    public UnauthorisedActionException(string message) : base(message)
    {
    }
}