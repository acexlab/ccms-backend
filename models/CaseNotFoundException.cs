using System;

namespace ccms_backend.models;

public class CaseNotFoundException : Exception
{
    public CaseNotFoundException(string message) : base(message)
    {
    }
}