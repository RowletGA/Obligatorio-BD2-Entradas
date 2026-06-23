using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using TicketingMundial.Application.Abstractions.Security;
using TicketingMundial.Application.DTOs;
using TicketingMundial.Application.Options;
using TicketingMundial.Domain.Identity;

namespace TicketingMundial.Infrastructure.Security;

public sealed class QrTokenService(IOptions<QrSecurityOptions> options, TimeProvider timeProvider) : IQrTokenService
{
    private const string Version = "v1";
    private const string GrantVersion = "g1";
    private const int SegmentCount = 6;
    private const int GrantSegmentCount = 6;
    private readonly QrSecurityOptions options = options.Value;

    public int MaxTokenLength => 255;

    public QrTokenGenerado Generar(QrTokenContext contexto)
    {
        var now = timeProvider.GetUtcNow();
        var lifetime = GetLifetime();
        var window = now.ToUnixTimeSeconds() / lifetime;
        var ownerMark = CreateOwnerMark(contexto.Propietario);
        var signingInput = BuildSigningInput(contexto.IdEntrada, contexto.IdEvento, window, ownerMark);
        var signature = Sign(signingInput);
        var windowEnds = DateTimeOffset.FromUnixTimeSeconds((window + 1) * lifetime);

        return new QrTokenGenerado
        {
            Token = $"{signingInput}.{signature}",
            VenceUtc = windowEnds,
            SegundosRestantes = Math.Max(0, (int)Math.Ceiling((windowEnds - now).TotalSeconds))
        };
    }

    public ResultadoValidacionQr LeerPayload(string token)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > MaxTokenLength)
        {
            return ResultadoValidacionQr.Rechazado("Formato inválido.");
        }

        var parts = token.Trim().Split('.');
        if (parts.Length != SegmentCount)
        {
            return ResultadoValidacionQr.Rechazado("Formato inválido.");
        }

        if (!string.Equals(parts[0], Version, StringComparison.Ordinal))
        {
            return ResultadoValidacionQr.Rechazado("Versión de QR no soportada.");
        }

        if (!ulong.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var idEntrada) ||
            !ulong.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out var idEvento) ||
            !long.TryParse(parts[3], NumberStyles.None, CultureInfo.InvariantCulture, out var window) ||
            idEntrada == 0 || idEvento == 0 || window < 0 ||
            !IsBase64Url(parts[4]) || !IsBase64Url(parts[5]))
        {
            return ResultadoValidacionQr.Rechazado("Formato inválido.");
        }

        return ResultadoValidacionQr.Valido(new QrTokenPayload
        {
            IdEntrada = idEntrada,
            IdEvento = idEvento,
            VentanaTemporal = window,
            MarcaPropietario = parts[4]
        });
    }

    public ResultadoValidacionQr Validar(string token, QrTokenValidationContext contexto)
    {
        var parsed = LeerPayload(token);
        if (!parsed.EsValido || parsed.Payload is null)
        {
            return parsed;
        }

        var payload = parsed.Payload;
        if (payload.IdEntrada != contexto.IdEntrada || payload.IdEvento != contexto.IdEvento)
        {
            return ResultadoValidacionQr.Rechazado("QR alterado.", payload);
        }

        var expectedOwnerMark = CreateOwnerMark(contexto.PropietarioActual);
        if (!FixedTimeEquals(payload.MarcaPropietario, expectedOwnerMark))
        {
            return ResultadoValidacionQr.Rechazado("QR emitido para un propietario anterior.", payload);
        }

        var parts = token.Trim().Split('.');
        var signingInput = string.Join('.', parts.Take(SegmentCount - 1));
        var expectedSignature = Sign(signingInput);
        if (!FixedTimeEquals(parts[5], expectedSignature))
        {
            return ResultadoValidacionQr.Rechazado("QR alterado.", payload);
        }

        var nowSeconds = timeProvider.GetUtcNow().ToUnixTimeSeconds();
        var lifetime = GetLifetime();
        var skew = Math.Max(0, options.ClockSkewSeconds);
        var startsAt = payload.VentanaTemporal * lifetime;
        var endsAt = startsAt + lifetime;
        if (nowSeconds < startsAt - skew || nowSeconds > endsAt + skew)
        {
            return ResultadoValidacionQr.Rechazado("QR vencido.", payload);
        }

        return ResultadoValidacionQr.Valido(payload);
    }

    public QrGenerationGrant GenerarPermisoGeneracion(QrTokenContext contexto, TimeSpan lifetime)
    {
        var now = timeProvider.GetUtcNow();
        var expiresAt = now.Add(lifetime <= TimeSpan.Zero ? TimeSpan.FromMinutes(5) : lifetime);
        var ownerMark = CreateOwnerMark(contexto.Propietario);
        var expiresUnix = expiresAt.ToUnixTimeSeconds();
        var signingInput = string.Create(CultureInfo.InvariantCulture, $"{GrantVersion}.{contexto.IdEntrada}.{contexto.IdEvento}.{expiresUnix}.{ownerMark}");

        return new QrGenerationGrant
        {
            Grant = $"{signingInput}.{SignGrant(signingInput)}",
            VenceUtc = expiresAt
        };
    }

    public ResultadoValidacionPermisoQr ValidarPermisoGeneracion(string grant, DocumentoUsuario propietario)
    {
        if (string.IsNullOrWhiteSpace(grant) || grant.Length > MaxTokenLength)
        {
            return ResultadoValidacionPermisoQr.Rechazado("Permiso inválido.");
        }

        var parts = grant.Trim().Split('.');
        if (parts.Length != GrantSegmentCount ||
            !string.Equals(parts[0], GrantVersion, StringComparison.Ordinal) ||
            !ulong.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var idEntrada) ||
            !ulong.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out var idEvento) ||
            !long.TryParse(parts[3], NumberStyles.None, CultureInfo.InvariantCulture, out var expiresUnix) ||
            idEntrada == 0 || idEvento == 0 || expiresUnix <= 0 ||
            !IsBase64Url(parts[4]) || !IsBase64Url(parts[5]))
        {
            return ResultadoValidacionPermisoQr.Rechazado("Permiso inválido.");
        }

        var expectedOwnerMark = CreateOwnerMark(propietario);
        if (!FixedTimeEquals(parts[4], expectedOwnerMark))
        {
            return ResultadoValidacionPermisoQr.Rechazado("Permiso de otro propietario.");
        }

        var signingInput = string.Join('.', parts.Take(GrantSegmentCount - 1));
        if (!FixedTimeEquals(parts[5], SignGrant(signingInput)))
        {
            return ResultadoValidacionPermisoQr.Rechazado("Permiso alterado.");
        }

        var expiresAt = DateTimeOffset.FromUnixTimeSeconds(expiresUnix);
        if (timeProvider.GetUtcNow() > expiresAt)
        {
            return ResultadoValidacionPermisoQr.Rechazado("Permiso vencido.");
        }

        return ResultadoValidacionPermisoQr.Valido(new QrGenerationGrantPayload
        {
            IdEntrada = idEntrada,
            IdEvento = idEvento,
            MarcaPropietario = parts[4],
            VenceUtc = expiresAt
        });
    }

    private string CreateOwnerMark(DocumentoUsuario propietario)
    {
        var normalized = string.Join('|',
            propietario.TipoDocumento.Trim().ToUpperInvariant(),
            propietario.PaisDocumento.Trim().ToUpperInvariant(),
            propietario.NumeroDocumento.Trim());
        var full = ComputeHmac($"owner|{normalized}");
        return Base64UrlEncode(full.AsSpan(0, 16));
    }

    private string Sign(string signingInput) => Base64UrlEncode(ComputeHmac($"token|{signingInput}"));

    private string SignGrant(string signingInput) => Base64UrlEncode(ComputeHmac($"grant|{signingInput}"));

    private byte[] ComputeHmac(string value)
    {
        using var hmac = new HMACSHA256(GetSigningKey());
        return hmac.ComputeHash(Encoding.UTF8.GetBytes(value));
    }

    private byte[] GetSigningKey()
    {
        var key = options.SigningKey?.Trim() ?? string.Empty;
        try
        {
            var decoded = Convert.FromBase64String(key);
            if (decoded.Length >= 32)
            {
                return decoded;
            }
        }
        catch (FormatException)
        {
        }

        var raw = Encoding.UTF8.GetBytes(key);
        return raw.Length >= 32 ? raw : throw new InvalidOperationException("QrSecurity:SigningKey debe tener al menos 32 bytes.");
    }

    private int GetLifetime() => options.LifetimeSeconds > 0 ? options.LifetimeSeconds : 30;

    private static string BuildSigningInput(ulong idEntrada, ulong idEvento, long window, string ownerMark) =>
        string.Create(CultureInfo.InvariantCulture, $"{Version}.{idEntrada}.{idEvento}.{window}.{ownerMark}");

    private static bool FixedTimeEquals(string left, string right)
    {
        var leftBytes = Encoding.ASCII.GetBytes(left);
        var rightBytes = Encoding.ASCII.GetBytes(right);
        return leftBytes.Length == rightBytes.Length &&
            CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }

    private static bool IsBase64Url(string value) =>
        value.Length > 0 && value.All(c => char.IsLetterOrDigit(c) || c is '-' or '_');

    private static string Base64UrlEncode(ReadOnlySpan<byte> bytes) =>
        Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}
