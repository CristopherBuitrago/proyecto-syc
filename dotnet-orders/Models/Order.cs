namespace OrdersApi.Models;

public enum OrderStatus { Confirmed, Cancelled }

public class Order
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.Confirmed;
    public string? CouponCode { get; set; }
    public decimal Subtotal { get; set; }
    public decimal TotalDiscount { get; set; }
    public decimal TotalFinal { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
    public ICollection<AppliedDiscount> AppliedDiscounts { get; set; } = new List<AppliedDiscount>();
}

public class OrderItem
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public int ProductId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineDiscount { get; set; }
    public decimal LineTotal { get; set; }
}

public enum AppliedDiscountType { Volume, Loyalty, Coupon }

public class AppliedDiscount
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public AppliedDiscountType DiscountType { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}
