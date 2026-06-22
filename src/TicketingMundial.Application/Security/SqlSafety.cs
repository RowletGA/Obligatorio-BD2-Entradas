namespace TicketingMundial.Application.Security;

public static class SqlSafety
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    public static string EscapeLikePattern(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);
    }

    public static string ResolveEventoOrderColumn(string? order)
    {
        return order?.Trim().ToLowerInvariant() switch
        {
            "fecha" => "FechaHora",
            "estadio" => "Estadio",
            "equipo" => "EquipoLocal",
            _ => "FechaHora"
        };
    }

    public static string ResolveOrderDirection(string? direction)
    {
        return string.Equals(direction, "desc", StringComparison.OrdinalIgnoreCase)
            ? "DESC"
            : "ASC";
    }

    public static (int Page, int PageSize) NormalizePagination(int page, int pageSize)
    {
        var safePage = Math.Max(1, page);
        var safePageSize = pageSize <= 0
            ? DefaultPageSize
            : Math.Min(pageSize, MaxPageSize);

        return (safePage, safePageSize);
    }
}
