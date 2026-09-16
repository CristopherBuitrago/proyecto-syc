using OrdersApi.Models;

namespace OrdersApi.Services.Discounts;

public record OrderLineInput(int ProductId, string ProductName, int Quantity, decimal UnitPrice)
{
    public decimal LineSubtotal => Quantity * UnitPrice;
}

// Todo lo que el motor necesita para calcular — nada de acceso a BD acá adentro,
// así las pruebas unitarias no dependen de Postgres ni de mocks pesados.
public record DiscountContext(
    IReadOnlyList<OrderLineInput> Lines,
    decimal CustomerHistoricalTotal,
    Coupon? Coupon
);

public record LineDiscountResult(int ProductId, decimal LineSubtotal, decimal VolumeDiscountAmount, decimal LineTotalAfterVolume);

public record AppliedDiscountInfo(AppliedDiscountType Type, string Description, decimal Amount);

public record DiscountResult(
    decimal Subtotal,
    IReadOnlyList<LineDiscountResult> LineResults,
    decimal SubtotalAfterVolume,
    decimal LoyaltyDiscountAmount,
    decimal SubtotalAfterLoyalty,
    decimal CouponDiscountAmount,
    decimal TotalDiscount,
    decimal TotalFinal,
    IReadOnlyList<AppliedDiscountInfo> AppliedDiscounts
);
