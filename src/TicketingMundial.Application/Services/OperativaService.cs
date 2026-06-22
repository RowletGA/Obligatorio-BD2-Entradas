using TicketingMundial.Application.Abstractions.Repositories;
using TicketingMundial.Application.Abstractions.Services;
using TicketingMundial.Application.DTOs;
using TicketingMundial.Domain.Common;
using TicketingMundial.Domain.Identity;

namespace TicketingMundial.Application.Services;

public sealed class OperativaService(IOperativaRepository repository) : IOperativaService
{
    public async Task<OperationResult<CompraPreviewDto>> PreviewCompraAsync(ulong idEvento, IReadOnlyList<CompraSectorCantidad> cantidades, CancellationToken cancellationToken)
    {
        var validation = ValidateCantidades(cantidades);
        if (!validation.Success)
        {
            return OperationResult<CompraPreviewDto>.Failure(validation.Message ?? "Cantidades inválidas.");
        }

        return OperationResult<CompraPreviewDto>.Ok(await repository.ObtenerPreviewCompraAsync(idEvento, NormalizeCantidades(cantidades), cancellationToken));
    }

    public async Task<OperationResult<CompraResultadoDto>> ComprarAsync(DocumentoUsuario comprador, ulong idEvento, IReadOnlyList<CompraSectorCantidad> cantidades, CancellationToken cancellationToken)
    {
        var validation = ValidateCantidades(cantidades);
        if (!validation.Success)
        {
            return OperationResult<CompraResultadoDto>.Failure(validation.Message ?? "Cantidades inválidas.");
        }

        var result = await repository.ComprarAsync(NormalizeDocumento(comprador), idEvento, NormalizeCantidades(cantidades), cancellationToken);
        return OperationResult<CompraResultadoDto>.Ok(result, "Compra confirmada correctamente.");
    }

    public Task<IReadOnlyList<CompraResumenDto>> ListarComprasAsync(DocumentoUsuario comprador, CancellationToken cancellationToken) =>
        repository.ListarComprasAsync(NormalizeDocumento(comprador), cancellationToken);

    public Task<CompraDetalleDto?> ObtenerCompraAsync(DocumentoUsuario comprador, ulong idVenta, CancellationToken cancellationToken) =>
        repository.ObtenerCompraAsync(NormalizeDocumento(comprador), idVenta, cancellationToken);

    public Task<IReadOnlyList<EntradaResumenDto>> ListarEntradasPropiasAsync(DocumentoUsuario propietario, CancellationToken cancellationToken) =>
        repository.ListarEntradasPropiasAsync(NormalizeDocumento(propietario), cancellationToken);

    public Task<EntradaResumenDto?> ObtenerEntradaPropiaAsync(DocumentoUsuario propietario, ulong idEntrada, CancellationToken cancellationToken) =>
        repository.ObtenerEntradaPropiaAsync(NormalizeDocumento(propietario), idEntrada, cancellationToken);

    public Task<UsuarioDestinoDto?> BuscarUsuarioGeneralPorCorreoAsync(string correo, CancellationToken cancellationToken) =>
        repository.BuscarUsuarioGeneralPorCorreoAsync(correo.Trim(), cancellationToken);

    public async Task<OperationResult<ulong>> CrearTransferenciaAsync(DocumentoUsuario otorga, ulong idEntrada, string correoDestino, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(correoDestino))
        {
            return OperationResult<ulong>.Failure("Debe indicar el correo del destinatario.");
        }

        var id = await repository.CrearTransferenciaAsync(NormalizeDocumento(otorga), idEntrada, correoDestino.Trim(), cancellationToken);
        return OperationResult<ulong>.Ok(id, "Transferencia solicitada correctamente.");
    }

    public Task<IReadOnlyList<TransferenciaDto>> ListarTransferenciasEnviadasAsync(DocumentoUsuario usuario, CancellationToken cancellationToken) =>
        repository.ListarTransferenciasEnviadasAsync(NormalizeDocumento(usuario), cancellationToken);

    public Task<IReadOnlyList<TransferenciaDto>> ListarTransferenciasRecibidasAsync(DocumentoUsuario usuario, CancellationToken cancellationToken) =>
        repository.ListarTransferenciasRecibidasAsync(NormalizeDocumento(usuario), cancellationToken);

    public async Task<OperationResult> ResponderTransferenciaAsync(DocumentoUsuario usuario, ulong idTransferencia, string estado, bool receptor, CancellationToken cancellationToken)
    {
        var normalized = estado.Trim().ToUpperInvariant();
        if (normalized is not ("ACEPTADA" or "RECHAZADA" or "CANCELADA"))
        {
            return OperationResult.Failure("Estado de transferencia inválido.");
        }

        var updated = await repository.ResponderTransferenciaAsync(NormalizeDocumento(usuario), idTransferencia, normalized, receptor, cancellationToken);
        return updated ? OperationResult.Ok("Transferencia actualizada.") : OperationResult.Failure("No se encontró una transferencia pendiente para responder.");
    }

    public Task<IReadOnlyList<FuncionarioDto>> ListarFuncionariosAsync(CancellationToken cancellationToken) => repository.ListarFuncionariosAsync(cancellationToken);

    public Task<IReadOnlyList<AsignacionFuncionarioDto>> ListarAsignacionesAsync(string paisSede, CancellationToken cancellationToken) =>
        repository.ListarAsignacionesAsync(paisSede.Trim().ToUpperInvariant(), cancellationToken);

    public async Task<OperationResult> AsignarFuncionarioAsync(DocumentoUsuario funcionario, ulong idEvento, ulong idSector, string paisSede, CancellationToken cancellationToken)
    {
        if (idEvento == 0 || idSector == 0)
        {
            return OperationResult.Failure("Debe seleccionar evento y sector.");
        }

        await repository.AsignarFuncionarioAsync(NormalizeDocumento(funcionario), idEvento, idSector, paisSede.Trim().ToUpperInvariant(), cancellationToken);
        return OperationResult.Ok("Funcionario asignado correctamente.");
    }

    public Task<IReadOnlyList<AsignacionFuncionarioDto>> ListarAsignacionesFuncionarioAsync(DocumentoUsuario funcionario, CancellationToken cancellationToken) =>
        repository.ListarAsignacionesFuncionarioAsync(NormalizeDocumento(funcionario), cancellationToken);

    public async Task<OperationResult<ValidacionEntradaDto>> ValidarEntradaAsync(DocumentoUsuario funcionario, ulong idEntrada, string token, CancellationToken cancellationToken)
    {
        if (idEntrada == 0)
        {
            return OperationResult<ValidacionEntradaDto>.Failure("Debe indicar una entrada.");
        }

        var result = await repository.ValidarEntradaAsync(NormalizeDocumento(funcionario), idEntrada, string.IsNullOrWhiteSpace(token) ? $"DEMO-{idEntrada}" : token.Trim(), cancellationToken);
        return OperationResult<ValidacionEntradaDto>.Ok(result, "Entrada validada correctamente.");
    }

    public Task<IReadOnlyList<ReporteEventoVendidoDto>> ReporteEventosVendidosAsync(DateTime? desde, DateTime? hasta, int limite, CancellationToken cancellationToken) =>
        repository.ReporteEventosVendidosAsync(desde, hasta, ClampLimit(limite), cancellationToken);

    public Task<IReadOnlyList<ReporteCompradorDto>> ReporteCompradoresAsync(DateTime? desde, DateTime? hasta, int limite, CancellationToken cancellationToken) =>
        repository.ReporteCompradoresAsync(desde, hasta, ClampLimit(limite), cancellationToken);

    private static OperationResult ValidateCantidades(IReadOnlyList<CompraSectorCantidad> cantidades)
    {
        var total = cantidades.Where(item => item.Cantidad > 0).Sum(item => item.Cantidad);
        if (total <= 0)
        {
            return OperationResult.Failure("Debe seleccionar al menos una entrada.");
        }

        if (total > 5)
        {
            return OperationResult.Failure("Una compra no puede superar cinco entradas.");
        }

        if (cantidades.Any(item => item.Cantidad < 0))
        {
            return OperationResult.Failure("Las cantidades no pueden ser negativas.");
        }

        return OperationResult.Ok();
    }

    private static IReadOnlyList<CompraSectorCantidad> NormalizeCantidades(IReadOnlyList<CompraSectorCantidad> cantidades) =>
        cantidades
            .Where(item => item.Cantidad > 0)
            .GroupBy(item => item.IdSector)
            .Select(group => new CompraSectorCantidad(group.Key, group.Sum(item => item.Cantidad)))
            .ToArray();

    private static DocumentoUsuario NormalizeDocumento(DocumentoUsuario documento) =>
        new(documento.TipoDocumento.Trim().ToUpperInvariant(), documento.PaisDocumento.Trim().ToUpperInvariant(), documento.NumeroDocumento.Trim());

    private static int ClampLimit(int limite) => limite is < 1 or > 100 ? 10 : limite;
}
