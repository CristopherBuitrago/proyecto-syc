namespace OrdersApi.DTOs;

public record CouponResponse(
    string Code,
    string DiscountType,
    decimal DiscountValue,
    decimal MinPurchaseAmount,
    DateTimeOffset ExpiresAt,
    bool Stackable
);
