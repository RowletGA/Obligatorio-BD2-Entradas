namespace TicketingMundial.Domain.Common;

public sealed record OperationResult(bool Success, string? Message = null)
{
    public static OperationResult Ok(string? message = null) => new(true, message);

    public static OperationResult Failure(string message) => new(false, message);
}

public sealed record OperationResult<T>(bool Success, T? Value = default, string? Message = null)
{
    public static OperationResult<T> Ok(T value, string? message = null) => new(true, value, message);

    public static OperationResult<T> Failure(string message) => new(false, default, message);
}
