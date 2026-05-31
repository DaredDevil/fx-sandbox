namespace FxSandbox.Domain;

public sealed class Position
{
    public required string Pair { get; set; }
    public required OrderSide Side { get; set; }
    public decimal Quantity { get; set; }
    public decimal AverageEntryPrice { get; set; }
}
