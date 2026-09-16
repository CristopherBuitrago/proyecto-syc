namespace OrdersApi.Services.Discounts;

public class LoyaltyDiscountRule : ILoyaltyDiscountRule
{
    // El histórico se calcula en tiempo real desde las órdenes CONFIRMED existentes
    // (ver OrderService), no se guarda un campo "tier" en customers — así el cálculo
    // siempre es consistente con la última compra confirmada. Ver DECISIONS.md.
    public decimal CalculateDiscount(decimal subtotalAfterVolume, decimal customerHistoricalTotal)
    {
        decimal rate = GetRate(customerHistoricalTotal);
        return Math.Round(subtotalAfterVolume * rate, 2);
    }

    public string DescribeTier(decimal customerHistoricalTotal) => customerHistoricalTotal switch
    {
        >= 5_000_000 => "Platinum",
        >= 2_000_000 => "Gold",
        >= 500_000 => "Silver",
        _ => "Bronze"
    };

    private static decimal GetRate(decimal historicalTotal) => historicalTotal switch
    {
        >= 5_000_000 => 0.10m,
        >= 2_000_000 => 0.06m,
        >= 500_000 => 0.03m,
        _ => 0m
    };
}
