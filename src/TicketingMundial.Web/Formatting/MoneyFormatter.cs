using System.Globalization;

namespace TicketingMundial.Web.Formatting;

public static class MoneyFormatter
{
    private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("es-UY");

    public static string Format(decimal value)
    {
        return string.Create(Culture, $"$ {value:N2}");
    }
}
