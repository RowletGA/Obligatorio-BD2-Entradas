using TicketingMundial.Application.Abstractions.Authentication;
using TicketingMundial.Application.Abstractions.Repositories;
using TicketingMundial.Application.Abstractions.Services;
using TicketingMundial.Application.DTOs;
using TicketingMundial.Application.Validation;
using TicketingMundial.Domain.Common;

namespace TicketingMundial.Application.Services;

public sealed class UsuarioService(
    IUsuarioRepository usuarioRepository,
    IPasswordService passwordService,
    ICatalogoRegistroService catalogoRegistroService) : IUsuarioService
{
    public async Task<OperationResult> RegistrarUsuarioGeneralAsync(
        RegistroUsuarioCommand command,
        CancellationToken cancellationToken)
    {
        var passwordValidation = PasswordPolicyValidator.Validate(command.Password, command.ConfirmPassword);
        if (!passwordValidation.IsValid)
        {
            return OperationResult.Failure(string.Join(" ", passwordValidation.Errors));
        }

        var documentoValidation = catalogoRegistroService.ValidarDocumento(command.Documento);
        if (!documentoValidation.IsValid)
        {
            return OperationResult.Failure(string.Join(" ", documentoValidation.Errors));
        }

        if (!string.IsNullOrWhiteSpace(command.DireccionPais) &&
            !catalogoRegistroService.ObtenerPaises().Any(pais => pais.Codigo == command.DireccionPais.Trim().ToUpperInvariant()))
        {
            return OperationResult.Failure("El país de dirección no pertenece al catálogo permitido.");
        }

        var telefonosValidation = TelefonoValidator.ValidateAndNormalize(command.Telefonos);
        if (!telefonosValidation.IsValid)
        {
            return OperationResult.Failure(string.Join(" ", telefonosValidation.Errors));
        }

        var documento = documentoValidation.DocumentoNormalizado;
        var correo = command.CorreoElectronico.Trim();

        if (await usuarioRepository.ExisteCorreoAsync(correo, cancellationToken))
        {
            return OperationResult.Failure("Ya existe un usuario registrado con ese correo electrónico.");
        }

        if (await usuarioRepository.ExisteDocumentoAsync(documento, cancellationToken))
        {
            return OperationResult.Failure("Ya existe un usuario registrado con ese documento.");
        }

        var usuarioAuth = new UsuarioAutenticacion
        {
            Documento = documento,
            PrimerNombre = command.PrimerNombre,
            PrimerApellido = command.PrimerApellido,
            CorreoElectronico = correo
        };

        var hash = passwordService.HashPassword(usuarioAuth, command.Password);

        var registro = new RegistroUsuarioData
        {
            Documento = documento,
            PrimerNombre = command.PrimerNombre.Trim(),
            PrimerApellido = command.PrimerApellido.Trim(),
            CorreoElectronico = correo,
            DireccionPais = NullIfWhiteSpace(command.DireccionPais),
            DireccionLocalidad = NullIfWhiteSpace(command.DireccionLocalidad),
            DireccionCalle = NullIfWhiteSpace(command.DireccionCalle),
            DireccionNumero = NullIfWhiteSpace(command.DireccionNumero),
            DireccionCodigoPostal = NullIfWhiteSpace(command.DireccionCodigoPostal),
            Telefonos = telefonosValidation.TelefonosNormalizados
        };

        await usuarioRepository.RegistrarUsuarioGeneralAsync(registro, hash, cancellationToken);
        return OperationResult.Ok("Usuario registrado correctamente.");
    }

    private static string? NullIfWhiteSpace(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
