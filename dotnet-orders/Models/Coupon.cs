namespace OrdersApi.Models;

public enum DiscountType { Percent, Fixed }

public class Coupon
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public DiscountType DiscountType { get; set; }
    public decimal DiscountValue { get; set; }
    public decimal MinPurchaseAmount { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public bool Stackable { get; set; }
}
