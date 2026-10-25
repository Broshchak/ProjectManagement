using Application.Common.Exceptions;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Infrastructure.Persistence;

internal static class PersistenceErrors
{
    public static bool IsConflict(Exception exception) => exception is not DataConflictException &&
        (exception is DbUpdateConcurrencyException ||
         exception is PostgresException { SqlState: PostgresErrorCodes.SerializationFailure or
             PostgresErrorCodes.DeadlockDetected or PostgresErrorCodes.UniqueViolation or PostgresErrorCodes.ForeignKeyViolation } ||
         (exception.InnerException is not null && IsConflict(exception.InnerException)));

    public static DataConflictException Translate(Exception exception)
    {
        var cause = exception;
        while (cause.InnerException is not null && cause is not PostgresException)
            cause = cause.InnerException;
        string message = cause is PostgresException postgres ? postgres.SqlState switch
        {
            PostgresErrorCodes.UniqueViolation => "A record with these details already exists.",
            PostgresErrorCodes.ForeignKeyViolation => "Related data has changed or is still in use. Reload it before retrying.",
            _ => "Data changed during saving. Reload it before retrying."
        } : "Data changed during saving. Reload it before retrying.";
        return new DataConflictException(message, exception);
    }
}
