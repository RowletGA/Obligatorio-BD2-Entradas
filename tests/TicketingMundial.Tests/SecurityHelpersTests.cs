using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using TicketingMundial.Application.Abstractions.Repositories;
using TicketingMundial.Application.DTOs;
using TicketingMundial.Application.Security;
using TicketingMundial.Application.Services;
using TicketingMundial.Domain.Identity;
using TicketingMundial.Infrastructure.Authentication;
using TicketingMundial.Infrastructure.Errors;

namespace TicketingMundial.Tests;

public sealed class SecurityHelpersTests
{
    [Fact]
    public void CreateClaims_UsesOnlyExpectedIdentityAndRoles()
    {
        var usuario = new UsuarioAutenticadoDto
        {
            Documento = new DocumentoUsuario("CI", "Uruguay", "12345678"),
            PrimerNombre = "Ana",
            PrimerApellido = "Demo",
            CorreoElectronico = "ana@example.com",
            Roles = [RolesAplicacion.UsuarioGeneral, RolesAplicacion.Funcionario]
        };

        var claims = AuthenticationClaimsFactory.CreateClaims(usuario);

        Assert.Contains(claims, claim => claim.Type == ClaimTypes.Email && claim.Value == "ana@example.com");
        Assert.Contains(claims, claim => claim.Type == ClaimTypes.Role && claim.Value == RolesAplicacion.UsuarioGeneral);
        Assert.Contains(claims, claim => claim.Type == ClaimTypes.Role && claim.Value == RolesAplicacion.Funcionario);
        Assert.DoesNotContain(claims, claim => claim.Type.Contains("password", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("fecha", "FechaHora")]
    [InlineData("estadio", "Estadio")]
    [InlineData("equipo", "EquipoLocal")]
    [InlineData("IDEvento; DROP TABLE Usuario; --", "FechaHora")]
    public void ResolveEventoOrderColumn_UsesWhitelist(string input, string expected)
    {
        Assert.Equal(expected, SqlSafety.ResolveEventoOrderColumn(input));
    }

    [Theory]
    [InlineData("desc", "DESC")]
    [InlineData("DESC", "DESC")]
    [InlineData("desc; DROP TABLE Usuario", "ASC")]
    [InlineData("anything", "ASC")]
    public void ResolveOrderDirection_UsesWhitelist(string input, string expected)
    {
        Assert.Equal(expected, SqlSafety.ResolveOrderDirection(input));
    }

    [Fact]
    public void NormalizePagination_ClampsValues()
    {
        var result = SqlSafety.NormalizePagination(-5, 500);

        Assert.Equal(1, result.Page);
        Assert.Equal(SqlSafety.MaxPageSize, result.PageSize);
    }

    [Theory]
    [InlineData("' OR 1=1 --")]
    [InlineData("\" OR \"1\"=\"1")]
    [InlineData("'; DROP TABLE Usuario; --")]
    [InlineData("admin'--")]
    [InlineData("%' OR 1=1 --")]
    public void EscapeLikePattern_TreatsMaliciousInputAsText(string input)
    {
        var escaped = SqlSafety.EscapeLikePattern(input);

        if (input.Contains('%', StringComparison.Ordinal))
        {
            Assert.Contains("\\%", escaped);
        }

        if (input.Contains('_', StringComparison.Ordinal))
        {
            Assert.Contains("\\_", escaped);
        }

        Assert.DoesNotContain('\0', escaped);
        Assert.True(escaped.Length >= input.Length);
    }

    [Fact]
    public void TranslateMessage_ReturnsTriggerMessageForSqlState45000()
    {
        var message = MySqlExceptionTranslator.TranslateMessage("45000", 1644, "Mensaje funcional");

        Assert.Equal("Mensaje funcional", message);
    }

    [Theory]
    [InlineData("' OR 1=1 --")]
    [InlineData("\" OR \"1\"=\"1")]
    [InlineData("'; DROP TABLE Usuario; --")]
    [InlineData("admin'--")]
    [InlineData("%' OR 1=1 --")]
    public async Task AuthenticationService_DoesNotAuthenticateMaliciousEmailValues(string maliciousEmail)
    {
        var repository = new FakeUsuarioRepository();
        var service = new AuthenticationService(
            repository,
            new PasswordService(),
            NullLogger<AuthenticationService>.Instance);

        var result = await service.AuthenticateAsync(maliciousEmail, "Entrada2026", CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal("Correo o contraseña incorrectos.", result.ErrorMessage);
        Assert.Equal(maliciousEmail.Trim(), repository.LastEmail);
    }

    [Fact]
    public async Task AuthenticationService_DetectsProfilesFromRepository()
    {
        var passwordService = new PasswordService();
        var usuarioBase = new UsuarioAutenticacion
        {
            Documento = new DocumentoUsuario("CI", "Uruguay", "12345678"),
            CorreoElectronico = "ana@example.com",
            PrimerNombre = "Ana",
            PrimerApellido = "Demo",
            Roles = [RolesAplicacion.UsuarioGeneral, RolesAplicacion.Administrador]
        };
        var hash = passwordService.HashPassword(usuarioBase, "Entrada2026");
        var usuario = new UsuarioAutenticacion
        {
            Documento = usuarioBase.Documento,
            CorreoElectronico = usuarioBase.CorreoElectronico,
            PrimerNombre = usuarioBase.PrimerNombre,
            PrimerApellido = usuarioBase.PrimerApellido,
            Roles = usuarioBase.Roles,
            HashContrasena = hash
        };
        var repository = new FakeUsuarioRepository(usuario);
        var service = new AuthenticationService(
            repository,
            passwordService,
            NullLogger<AuthenticationService>.Instance);

        var result = await service.AuthenticateAsync("ana@example.com", "Entrada2026", CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Contains(RolesAplicacion.UsuarioGeneral, result.Usuario!.Roles);
        Assert.Contains(RolesAplicacion.Administrador, result.Usuario.Roles);
    }

    private sealed class FakeUsuarioRepository(UsuarioAutenticacion? usuario = null) : IUsuarioRepository
    {
        public string? LastEmail { get; private set; }

        public Task<UsuarioAutenticacion?> ObtenerParaAutenticacionAsync(
            string correoElectronico,
            CancellationToken cancellationToken)
        {
            LastEmail = correoElectronico;
            return Task.FromResult(
                string.Equals(usuario?.CorreoElectronico, correoElectronico, StringComparison.Ordinal)
                    ? usuario
                    : null);
        }

        public Task<bool> ExisteCorreoAsync(string correoElectronico, CancellationToken cancellationToken) =>
            Task.FromResult(false);

        public Task<bool> ExisteDocumentoAsync(DocumentoUsuario documento, CancellationToken cancellationToken) =>
            Task.FromResult(false);

        public Task RegistrarUsuarioGeneralAsync(
            RegistroUsuarioData usuario,
            string hashContrasena,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task ActualizarHashContrasenaAsync(
            DocumentoUsuario documento,
            string hashContrasena,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}
