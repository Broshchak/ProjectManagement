namespace Application.Common.Exceptions;

public sealed class WorkflowAccessException() : Exception("Your role cannot execute this transition.");
