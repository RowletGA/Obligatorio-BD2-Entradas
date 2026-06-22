namespace TicketingMundial.Application.DTOs;

public sealed record EventoFiltroDto(
    DateTime? Desde,
    DateTime? Hasta,
    ulong? IdEstadio,
    string? Equipo);
