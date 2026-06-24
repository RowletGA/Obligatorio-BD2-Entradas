namespace TicketingMundial.Web.Formatting;

public static class StatusBadgeHelper
{
    public static string Transferencia(string? estado) => Normalize(estado) switch
    {
        "ACEPTADA" => "status-badge status-badge-success",
        "RECHAZADA" => "status-badge status-badge-danger",
        "CANCELADA" => "status-badge status-badge-danger-strong",
        _ => "status-badge status-badge-muted"
    };

    public static string Venta(string? estado) => Normalize(estado) switch
    {
        "CONFIRMADA" => "status-badge status-badge-success",
        "PAGA" => "status-badge status-badge-success-strong",
        _ => "status-badge status-badge-muted"
    };

    public static string Entrada(string? estado) => Normalize(estado) switch
    {
        "ACTIVA" => "status-badge status-badge-info",
        "VALIDADA" => "status-badge status-badge-success-strong",
        "TRANSFERIDA" => "status-badge status-badge-muted",
        "ANULADA" => "status-badge status-badge-danger",
        _ => "status-badge status-badge-muted"
    };

    public static string Evento(string? estado) => Normalize(estado) switch
    {
        "PROGRAMADO" => "status-badge status-badge-info",
        "EN_CURSO" => "status-badge status-badge-success",
        "FINALIZADO" => "status-badge status-badge-dark",
        "CANCELADO" => "status-badge status-badge-danger",
        _ => "status-badge status-badge-muted"
    };

    private static string Normalize(string? estado) => estado?.Trim().ToUpperInvariant() ?? string.Empty;
}
