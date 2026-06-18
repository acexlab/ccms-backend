using CCMS.Domain.Enums;
/*
 * File: CaseAlreadyRespondedException.cs
 * Description: Exception thrown when trying to respond to or modify a case that has already received a response.
 * To Implement: Handled by exception middleware to return a 409 Conflict HTTP status code.
 */