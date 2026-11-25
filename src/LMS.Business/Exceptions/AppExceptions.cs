namespace LMS.Business.Exceptions;

public abstract class AppException : Exception
{
    public int StatusCode { get; }
    public Dictionary<string, string[]>? Errors { get; }

    protected AppException(string message, int statusCode, Dictionary<string, string[]>? errors = null)
        : base(message)
    {
        StatusCode = statusCode;
        Errors = errors;
    }
}

public class ValidationException : AppException
{
    public ValidationException(string message, Dictionary<string, string[]>? errors = null)
        : base(message, 400, errors)
    {
    }
}

public class UnauthorizedException : AppException
{
    public UnauthorizedException(string message) : base(message, 401)
    {
    }
}

public class ForbiddenException : AppException
{
    public ForbiddenException(string message) : base(message, 403)
    {
    }
}

public class NotFoundException : AppException
{
    public NotFoundException(string message) : base(message, 404)
    {
    }
}

public class ConflictException : AppException
{
    public ConflictException(string message) : base(message, 409)
    {
    }
}
