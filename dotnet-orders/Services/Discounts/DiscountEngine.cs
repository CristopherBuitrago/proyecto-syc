using OrdersApi.Models;

namespace OrdersApi.Services.Discounts;

/// <summary>
/// Orquestador del motor de descuentos. No valida el cupón (existencia, expiración,
/// monto mínimo) — eso ya se resolvió antes de llegar acá (ver OrderService), así este
/// motor es puro cálculo y fácil de probar unitariamente sin tocar la base de datos.
///
/// Orden de aplicación (ver DECISIONS.md para la justificación completa):
///   1. Volumen (por línea)
///   2. Loyalty (sobre el subtotal ya con volumen aplicado)
///   3. Cupón:
///      - stackable = true  -> se suma encima de volumen + loyalty ya aplicados.
///      - stackable = false -> compite contra "volumen + loyalty combinados" y gana
///        la opción que le cueste MENOS al cliente (más beneficiosa para él).
/// </summary>
public class DiscountEngine
{
    private readonly IVolumeDiscountRule _volumeRule;
    private readonly ILoyaltyDiscountRule _loyaltyRule;

    public DiscountEngine(IVolumeDiscountRule volumeRule, ILoyaltyDiscountRule loyaltyRule)
    {
        _volumeRule = volumeRule;
        _loyaltyRule = loyaltyRule;
    }

    public DiscountResult Calculate(DiscountContext context)
    {
        if (context.Lines.Count == 0)
            throw new ArgumentException("El pedido debe tener al menos una línea de producto.");

        var lineResults = context.Lines.Select(_volumeRule.ApplyToLine).ToList();

        decimal subtotal = lineResults.Sum(l => l.LineSubtotal);
        decimal subtotalAfterVolume = lineResults.Sum(l => l.LineTotalAfterVolume);
        decimal volumeDiscountTotal = subtotal - subtotalAfterVolume;

        decimal loyaltyDiscount = _loyaltyRule.CalculateDiscount(subtotalAfterVolume, context.CustomerHistoricalTotal);
        decimal subtotalAfterLoyalty = subtotalAfterVolume - loyaltyDiscount;

        var appliedDiscounts = new List<AppliedDiscountInfo>();
        if (volumeDiscountTotal > 0)
            appliedDiscounts.Add(new AppliedDiscountInfo(AppliedDiscountType.Volume, "Descuento por volumen (10-19u: 5%, 20+u: 10%) por línea", volumeDiscountTotal));
        if (loyaltyDiscount > 0)
            appliedDiscounts.Add(new AppliedDiscountInfo(AppliedDiscountType.Loyalty, $"Descuento por nivel de cliente ({_loyaltyRule.DescribeTier(context.CustomerHistoricalTotal)})", loyaltyDiscount));

        // Sin cupón: el resultado es simplemente volumen + loyalty.
        if (context.Coupon is null)
        {
            return new DiscountResult(
                subtotal, lineResults, subtotalAfterVolume,
                loyaltyDiscount, subtotalAfterLoyalty,
                CouponDiscountAmount: 0,
                TotalDiscount: subtotal - subtotalAfterLoyalty,
                TotalFinal: subtotalAfterLoyalty,
                appliedDiscounts);
        }

        var coupon = context.Coupon;

        if (coupon.Stackable)
        {
            decimal couponAmount = CalculateCouponAmount(coupon, subtotalAfterLoyalty);
            decimal finalTotal = subtotalAfterLoyalty - couponAmount;

            if (couponAmount > 0)
                appliedDiscounts.Add(new AppliedDiscountInfo(AppliedDiscountType.Coupon, $"Cupón {coupon.Code} (combinable)", couponAmount));

            return new DiscountResult(
                subtotal, lineResults, subtotalAfterVolume,
                loyaltyDiscount, subtotalAfterLoyalty,
                couponAmount,
                TotalDiscount: subtotal - finalTotal,
                TotalFinal: finalTotal,
                appliedDiscounts);
        }

        // No combinable: compite "volumen + loyalty" vs "solo cupón" sobre el subtotal
        // crudo, y gana la que le cueste menos al cliente.
        decimal couponOnlyAmount = CalculateCouponAmount(coupon, subtotal);
        decimal couponOnlyFinalTotal = subtotal - couponOnlyAmount;

        bool couponIsBetter = couponOnlyFinalTotal < subtotalAfterLoyalty;

        if (couponIsBetter)
        {
            var couponOnlyLineResults = context.Lines
                .Select(l => new LineDiscountResult(l.ProductId, l.LineSubtotal, 0, l.LineSubtotal))
                .ToList();

            return new DiscountResult(
                subtotal, couponOnlyLineResults, subtotal,
                LoyaltyDiscountAmount: 0, SubtotalAfterLoyalty: subtotal,
                couponOnlyAmount,
                TotalDiscount: couponOnlyAmount,
                TotalFinal: couponOnlyFinalTotal,
                new List<AppliedDiscountInfo>
                {
                    new(AppliedDiscountType.Coupon, $"Cupón {coupon.Code} (no combinable — reemplazó volumen/loyalty por ser más beneficioso)", couponOnlyAmount)
                });
        }

        // Volumen + loyalty ya eran mejores que el cupón — el cupón simplemente no se aplica.
        return new DiscountResult(
            subtotal, lineResults, subtotalAfterVolume,
            loyaltyDiscount, subtotalAfterLoyalty,
            CouponDiscountAmount: 0,
            TotalDiscount: subtotal - subtotalAfterLoyalty,
            TotalFinal: subtotalAfterLoyalty,
            appliedDiscounts);
    }

    private static decimal CalculateCouponAmount(Coupon coupon, decimal baseAmount)
    {
        decimal amount = coupon.DiscountType == DiscountType.Percent
            ? baseAmount * (coupon.DiscountValue / 100m)
            : coupon.DiscountValue;

        // Nunca puede descontar más de lo que hay que pagar.
        return Math.Round(Math.Min(amount, baseAmount), 2);
    }
}
