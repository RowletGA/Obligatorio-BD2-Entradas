using MySqlConnector;

namespace TicketingMundial.Infrastructure.Data;

internal static class MySqlDataReaderExtensions
{
    public static string? GetNullableString(this MySqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    public static string GetRequiredString(this MySqlDataReader reader, string name)
    {
        return reader.GetString(reader.GetOrdinal(name));
    }

    public static ulong GetUInt64Value(this MySqlDataReader reader, string name)
    {
        return Convert.ToUInt64(reader.GetValue(reader.GetOrdinal(name)));
    }

    public static uint GetUInt32Value(this MySqlDataReader reader, string name)
    {
        return Convert.ToUInt32(reader.GetValue(reader.GetOrdinal(name)));
    }

    public static long GetInt64Value(this MySqlDataReader reader, string name)
    {
        return Convert.ToInt64(reader.GetValue(reader.GetOrdinal(name)));
    }

    public static decimal GetDecimalValue(this MySqlDataReader reader, string name)
    {
        return reader.GetDecimal(reader.GetOrdinal(name));
    }

    public static DateTime GetDateTimeValue(this MySqlDataReader reader, string name)
    {
        return reader.GetDateTime(reader.GetOrdinal(name));
    }

    public static DateTime? GetNullableDateTime(this MySqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetDateTime(ordinal);
    }

    public static bool GetBooleanValue(this MySqlDataReader reader, string name)
    {
        return Convert.ToBoolean(reader.GetValue(reader.GetOrdinal(name)));
    }
}
