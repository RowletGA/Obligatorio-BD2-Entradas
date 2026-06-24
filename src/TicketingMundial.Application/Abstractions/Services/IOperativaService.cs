using TicketingMundial.Application.DTOs;
using TicketingMundial.Domain.Common;
using TicketingMundial.Domain.Identity;

namespace TicketingMundial.Application.Abstractions.Services;

public interface IOperativaService
{
    Task<OperationResult<CompraPreviewDto>> PreviewCompraAsync(ulong idEvento, IReadOnlyList<CompraSectorCantidad> cantidades, CancellationToken cancellationToken);
    Task<OperationResult<CompraResultadoDto>> ComprarAsync(DocumentoUsuario comprador, ulong idEvento, IReadOnlyList<CompraSectorCantidad> cantidades, CancellationToken cancellationToken);
    Task<IReadOnlyList<CompraResumenDto>> ListarComprasAsync(DocumentoUsuario comprador, CancellationToken cancellationToken);
    Task<PagedResult<CompraResumenDto>> ListarComprasAsync(DocumentoUsuario comprador, CompraListQuery query, CancellationToken cancellationToken);
    Task<CompraDetalleDto?> ObtenerCompraAsync(DocumentoUsuario comprador, ulong idVenta, CancellationToken cancellationToken);
    Task<IReadOnlyList<EntradaResumenDto>> ListarEntradasPropiasAsync(DocumentoUsuario propietario, CancellationToken cancellationToken);
    Task<PagedResult<EntradaResumenDto>> ListarEntradasPropiasAsync(DocumentoUsuario propietario, EntradaListQuery query, CancellationToken cancellationToken);
    Task<EntradaResumenDto?> ObtenerEntradaPropiaAsync(DocumentoUsuario propietario, ulong idEntrada, CancellationToken cancellationToken);
    Task<OperationResult<QrEntradaGeneradoDto>> GenerarQrEntradaAsync(DocumentoUsuario propietario, ulong idEntrada, string? generationGrant, CancellationToken cancellationToken);
    Task<UsuarioDestinoDto?> BuscarUsuarioGeneralPorCorreoAsync(string correo, CancellationToken cancellationToken);
    Task<OperationResult<ulong>> CrearTransferenciaAsync(DocumentoUsuario otorga, ulong idEntrada, string correoDestino, CancellationToken cancellationToken);
    Task<IReadOnlyList<TransferenciaDto>> ListarTransferenciasEnviadasAsync(DocumentoUsuario usuario, CancellationToken cancellationToken);
    Task<IReadOnlyList<TransferenciaDto>> ListarTransferenciasRecibidasAsync(DocumentoUsuario usuario, CancellationToken cancellationToken);
    Task<PagedResult<TransferenciaDto>> ListarTransferenciasAsync(DocumentoUsuario usuario, TransferenciaListQuery query, CancellationToken cancellationToken);
    Task<OperationResult> ResponderTransferenciaAsync(DocumentoUsuario usuario, ulong idTransferencia, string estado, bool receptor, CancellationToken cancellationToken);
    Task<IReadOnlyList<FuncionarioDto>> ListarFuncionariosAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<AsignacionFuncionarioDto>> ListarAsignacionesAsync(string paisSede, CancellationToken cancellationToken);
    Task<OperationResult> AsignarFuncionarioAsync(DocumentoUsuario funcionario, ulong idEvento, ulong idSector, string paisSede, CancellationToken cancellationToken);
    Task<IReadOnlyList<AsignacionFuncionarioDto>> ListarAsignacionesFuncionarioAsync(DocumentoUsuario funcionario, CancellationToken cancellationToken);
    Task<OperationResult<ValidacionEntradaDto>> ValidarQrAsync(DocumentoUsuario funcionario, string token, CancellationToken cancellationToken);
    Task<IReadOnlyList<ReporteEventoVendidoDto>> ReporteEventosVendidosAsync(DateTime? desde, DateTime? hasta, int limite, CancellationToken cancellationToken);
    Task<IReadOnlyList<ReporteCompradorDto>> ReporteCompradoresAsync(DateTime? desde, DateTime? hasta, int limite, CancellationToken cancellationToken);
    Task<IReadOnlyList<ReporteValidacionesFuncionarioDto>> ReporteValidacionesPorFuncionarioAsync(ulong? idEvento, string? funcionario, DateTime? desde, DateTime? hasta, CancellationToken cancellationToken);
    Task<IReadOnlyList<ReporteTransferidorDto>> ReporteTransferidoresAsync(DateTime? desde, DateTime? hasta, int limite, CancellationToken cancellationToken);
}
