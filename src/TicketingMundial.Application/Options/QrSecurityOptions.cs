namespace TicketingMundial.Application.Options;

public sealed class QrSecurityOptions
{
    public const string SectionName = "QrSecurity";

    public string SigningKey { get; init; } = string.Empty;
    public int LifetimeSeconds { get; init; } = 30;
    public int ClockSkewSeconds { get; init; } = 5;
}
