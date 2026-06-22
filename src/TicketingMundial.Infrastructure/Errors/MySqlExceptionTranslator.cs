using Microsoft.Extensions.Logging;
using MySqlConnector;

namespace TicketingMundial.Infrastructure.Errors;

public interface IMySqlExceptionTranslator
{
    DatabaseException Translate(MySqlException exception, string operation);
}

public sealed class MySqlExceptionTranslator(ILogger<MySqlExceptionTranslator> logger)
    : IMySqlExceptionTranslator
{
    public DatabaseException Translate(MySqlException exception, string operation)
    {
        logger.LogError(exception, "Error MySQL durante {Operation}. Number={Number}, SqlState={SqlState}",
            operation,
            exception.Number,
            exception.SqlState);

        var message = TranslateMessage(exception.SqlState, exception.Number, exception.Message);

        return new DatabaseException(message, exception);
    }

    public static string TranslateMessage(string? sqlState, int number, string message)
    {
        return sqlState == "45000"
            ? message
            : number switch
            {
                1062 => "Ya existe un registro con esos datos.",
                1451 => "No se puede eliminar o modificar el registro porque tiene datos relacionados.",
                1452 => "La operacion referencia datos que no existen o no estan relacionados.",
                1042 => "La base de datos no esta disponible en este momento.",
                1045 => "No fue posible autenticar contra la base de datos.",
                1049 => "La base de datos configurada no existe o no esta disponible.",
                1205 => "La operacion demoro demasiado y fue cancelada.",
                1213 => "La operacion encontro un bloqueo transaccional. Intente nuevamente.",
                _ => "No fue posible completar la operacion en la base de datos."
            };
    }
}
