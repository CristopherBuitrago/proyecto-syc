namespace OrdersApi.Services.Discounts;

public class VolumeDiscountRule : IVolumeDiscountRule
{
    public LineDiscountResult ApplyToLine(OrderLineInput line)
    {
        decimal rate = line.Quantity switch
        {
            >= 20 => 0.10m,
            >= 10 => 0.05m,
            _ => 0m
        };

        decimal discountAmount = Math.Round(line.LineSubtotal * rate, 2);
        decimal totalAfterDiscount = line.LineSubtotal - discountAmount;

        return new LineDiscountResult(line.ProductId, line.LineSubtotal, discountAmount, totalAfterDiscount);
    }
}
