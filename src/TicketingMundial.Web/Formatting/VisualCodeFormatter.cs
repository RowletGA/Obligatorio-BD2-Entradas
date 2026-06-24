namespace TicketingMundial.Web.Formatting;

public static class VisualCodeFormatter
{
    public static string Entrada(int numero) => Format("ENT", numero);

    public static string Venta(int numero) => Format("VENT", numero);

    public static string Transferencia(int numero) => Format("TRF", numero);

    private static string Format(string prefix, int numero) => numero > 0 ? $"{prefix}-{numero:000}" : $"{prefix}-?";
}
