using System;

namespace ccms_backend.models;

public class CaseAlreadyRespondedException : Exception
{
    public CaseAlreadyRespondedException(string message) : base(message)
    {
    }
}