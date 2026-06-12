namespace ZohoWhatsAppAI.Application.Common.Exceptions;

public class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message)
    {
    }
}

public class ExternalServiceException : Exception
{
    public ExternalServiceException(string message, int statusCode = 502)
        : base(message)
    {
        StatusCode = statusCode;
    }

    public int StatusCode { get; }
}

public class ConfigurationException : Exception
{
    public ConfigurationException(string message) : base(message)
    {
    }
}
