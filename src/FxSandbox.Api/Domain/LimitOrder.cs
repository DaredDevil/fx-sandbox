namespace FxSandbox.Domain;

public sealed class LimitOrder
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string Pair { get; init; }
    public required OrderSide Side { get; init; }
    public required decimal LimitPrice { get; init; }
    public required decimal Quantity { get; init; }
    public OrderStatus Status { get; set; } = OrderStatus.Pending;
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime? FilledAt { get; set; }
}
