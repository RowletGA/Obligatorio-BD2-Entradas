namespace TicketingMundial.Web.Formatting;

public sealed record CountryDisplay(string Codigo, string Nombre, string Visual, bool EsBandera);

public static class CountryDisplayHelper
{
    public static CountryDisplay FromCode(string? code)
    {
        var normalized = (code ?? string.Empty).Trim().ToUpperInvariant();
        if (normalized.Length == 2 && normalized.All(c => c is >= 'A' and <= 'Z'))
        {
            var chars = normalized.Select(c => char.ConvertFromUtf32(0x1F1E6 + c - 'A'));
            return new CountryDisplay(normalized, normalized, string.Concat(chars), true);
        }

        var fallback = normalized.Length >= 2 ? normalized[..2] : "--";
        return new CountryDisplay(normalized, normalized, fallback, false);
    }
}
