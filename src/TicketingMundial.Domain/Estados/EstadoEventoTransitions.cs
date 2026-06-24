namespace TicketingMundial.Domain.Estados;

public static class EstadoEventoTransitions
{
    public static bool EsCerrado(string? estado) =>
        string.Equals(estado, EstadoEvento.Finalizado, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(estado, EstadoEvento.Cancelado, StringComparison.OrdinalIgnoreCase);

    public static IReadOnlyList<string> ObtenerDestinosPermitidos(string? estadoActual)
    {
        var estado = Normalize(estadoActual);
        return estado switch
        {
            EstadoEvento.Programado => [EstadoEvento.EnCurso, EstadoEvento.Cancelado],
            EstadoEvento.EnCurso => [EstadoEvento.Finalizado, EstadoEvento.Cancelado],
            _ => []
        };
    }

    public static bool PuedeTransicionar(string? estadoActual, string? estadoDestino) =>
        ObtenerDestinosPermitidos(estadoActual).Contains(Normalize(estadoDestino), StringComparer.Ordinal);

    public static string? ObtenerMotivoRechazo(string? estadoActual, string? estadoDestino)
    {
        var actual = Normalize(estadoActual);
        var destino = Normalize(estadoDestino);
        if (string.IsNullOrWhiteSpace(actual) || string.IsNullOrWhiteSpace(destino))
        {
            return "El estado del evento no es válido.";
        }

        if (actual == destino)
        {
            return "El evento ya se encuentra en ese estado.";
        }

        if (EsCerrado(actual))
        {
            return "Los eventos finalizados o cancelados no pueden cambiar de estado.";
        }

        return PuedeTransicionar(actual, destino)
            ? null
            : $"No se permite cambiar un evento de {actual} a {destino}.";
    }

    private static string Normalize(string? estado) => (estado ?? string.Empty).Trim().ToUpperInvariant();
}
