namespace Application.Common.Exceptions;

/// <summary>A provider-independent conflict that the API can safely expose.</summary>
public sealed class DataConflictException(string message, Exception? innerException = null)
    : Exception(message, innerException);
