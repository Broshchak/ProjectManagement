using System.Net;
using System.Net.Http;

namespace WpfClient.Services;

public sealed class ApiException(string message, HttpStatusCode statusCode)
    : HttpRequestException(message, null, statusCode);
