using TicketingMundial.Application.DTOs;
using TicketingMundial.Domain.Identity;

namespace TicketingMundial.Application.Abstractions.Repositories;

public interface IOperativaRepository
{
    Task<CompraPreviewDto> ObtenerPreviewCompraAsync(ulong idEvento, IReadOnlyList<CompraSectorCantidad> cantidades, CancellationToken cancellationToken);
    Task<CompraResultadoDto> ComprarAsync(DocumentoUsuario comprador, ulong idEvento, IReadOnlyList<CompraSectorCantidad> cantidades, CancellationToken cancellationToken);
    Task<IReadOnlyList<CompraResumenDto>> ListarComprasAsync(DocumentoUsuario comprador, CancellationToken cancellationToken);
    Task<PagedResult<CompraResumenDto>> ListarComprasAsync(DocumentoUsuario comprador, CompraListQuery query, CancellationToken cancellationToken);
    Task<CompraDetalleDto?> ObtenerCompraAsync(DocumentoUsuario comprador, ulong idVenta, CancellationToken cancellationToken);
    Task<IReadOnlyList<EntradaResumenDto>> ListarEntradasPropiasAsync(DocumentoUsuario propietario, CancellationToken cancellationToken);
    Task<PagedResult<EntradaResumenDto>> ListarEntradasPropiasAsync(DocumentoUsuario propietario, EntradaListQuery query, CancellationToken cancellationToken);
    Task<EntradaResumenDto?> ObtenerEntradaPropiaAsync(DocumentoUsuario propietario, ulong idEntrada, CancellationToken cancellationToken);
    Task<EntradaQrDto?> ObtenerEntradaQrAsync(DocumentoUsuario propietario, ulong idEntrada, CancellationToken cancellationToken);
    Task<UsuarioDestinoDto?> BuscarUsuarioGeneralPorCorreoAsync(string correo, CancellationToken cancellationToken);
    Task<ulong> CrearTransferenciaAsync(DocumentoUsuario otorga, ulong idEntrada, string correoDestino, CancellationToken cancellationToken);
    Task<IReadOnlyList<TransferenciaDto>> ListarTransferenciasEnviadasAsync(DocumentoUsuario usuario, CancellationToken cancellationToken);
    Task<IReadOnlyList<TransferenciaDto>> ListarTransferenciasRecibidasAsync(DocumentoUsuario usuario, CancellationToken cancellationToken);
    Task<PagedResult<TransferenciaDto>> ListarTransferenciasAsync(DocumentoUsuario usuario, TransferenciaListQuery query, CancellationToken cancellationToken);
    Task<bool> ResponderTransferenciaAsync(DocumentoUsuario usuario, ulong idTransferencia, string estado, bool receptor, CancellationToken cancellationToken);
    Task<IReadOnlyList<FuncionarioDto>> ListarFuncionariosAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<AsignacionFuncionarioDto>> ListarAsignacionesAsync(string paisSede, CancellationToken cancellationToken);
    Task<bool> ExisteFuncionarioAsync(DocumentoUsuario funcionario, CancellationToken cancellationToken);
    Task<EventoAdminDto?> ObtenerEventoAsignableAsync(ulong idEvento, string paisSede, CancellationToken cancellationToken);
    Task<bool> SectorHabilitadoParaEventoAsync(ulong idEvento, ulong idSector, string paisSede, CancellationToken cancellationToken);
    Task<bool> ExisteAsignacionFuncionarioAsync(DocumentoUsuario funcionario, ulong idEvento, ulong idSector, CancellationToken cancellationToken);
    Task AsignarFuncionarioAsync(DocumentoUsuario funcionario, ulong idEvento, ulong idSector, string paisSede, CancellationToken cancellationToken);
    Task<IReadOnlyList<AsignacionFuncionarioDto>> ListarAsignacionesFuncionarioAsync(DocumentoUsuario funcionario, CancellationToken cancellationToken);
    Task<ValidacionEntradaDto> ValidarEntradaQrAsync(DocumentoUsuario funcionario, string token, CancellationToken cancellationToken);
    Task<IReadOnlyList<ReporteEventoVendidoDto>> ReporteEventosVendidosAsync(DateTime? desde, DateTime? hasta, int limite, CancellationToken cancellationToken);
    Task<IReadOnlyList<ReporteCompradorDto>> ReporteCompradoresAsync(DateTime? desde, DateTime? hasta, int limite, CancellationToken cancellationToken);
    Task<IReadOnlyList<ReporteValidacionesFuncionarioDto>> ReporteValidacionesPorFuncionarioAsync(ulong? idEvento, string? funcionario, DateTime? desde, DateTime? hasta, CancellationToken cancellationToken);
    Task<IReadOnlyList<ReporteTransferidorDto>> ReporteTransferidoresAsync(DateTime? desde, DateTime? hasta, int limite, CancellationToken cancellationToken);
}
