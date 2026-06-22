namespace TicketingMundial.Infrastructure.Errors;

public sealed class DatabaseException(string userMessage, Exception innerException)
    : Exception(userMessage, innerException)
{
    public string UserMessage { get; } = userMessage;
}
