using TicketingMundial.Web.Formatting;

namespace TicketingMundial.Tests;

public sealed class MoneyFormatterTests
{
    [Fact]
    public void Format_UsaFormatoMonetarioConsistente()
    {
        var value = MoneyFormatter.Format(1250.5m);

        Assert.Equal("$ 1.250,50", value);
    }
}
