namespace Application.Common.Exceptions
{
    public class ServiceBusyException(string message) : Exception(message);
}
