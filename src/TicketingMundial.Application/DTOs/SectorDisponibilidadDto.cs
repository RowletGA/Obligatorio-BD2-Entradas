namespace TicketingMundial.Application.DTOs;

public sealed class SectorDisponibilidadDto
{
    public ulong IdEvento { get; init; }
    public ulong IdSector { get; init; }
    public string NombreSector { get; init; } = string.Empty;
    public uint Capacidad { get; init; }
    public decimal PrecioBase { get; init; }
    public long EntradasEmitidas { get; init; }
    public long LugaresDisponibles { get; init; }
}
