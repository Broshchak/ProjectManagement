namespace Application.Common.Exceptions;

public sealed class ReportAccessException() : Exception("You do not have access to the data required by this report.");
