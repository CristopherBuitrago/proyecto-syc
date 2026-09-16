using OrdersApi.Models;
using OrdersApi.Services.Discounts;
using Xunit;

namespace OrdersApi.Tests;

public class DiscountEngineTests
{
    private readonly DiscountEngine _engine = new(new VolumeDiscountRule(), new LoyaltyDiscountRule());

    private static Coupon MakeCoupon(string code, DiscountType type, decimal value, bool stackable, decimal minPurchase = 0)
        => new()
        {
            Code = code,
            DiscountType = type,
            DiscountValue = value,
            MinPurchaseAmount = minPurchase,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(30),
            Stackable = stackable
        };

    [Fact]
    public void SinDescuentos_ClienteBronze_SinCupon_NoAplicaNadaDeDescuento()
    {
        var lines = new[] { new OrderLineInput(1, "Producto A", 5, 10_000m) };
        var context = new DiscountContext(lines, CustomerHistoricalTotal: 0, Coupon: null);

        var result = _engine.Calculate(context);

        Assert.Equal(50_000m, result.Subtotal);
        Assert.Equal(0, result.TotalDiscount);
        Assert.Equal(50_000m, result.TotalFinal);
        Assert.Empty(result.AppliedDiscounts);
    }

    [Theory]
    [InlineData(9, 0)]      // debajo del umbral -> sin descuento
    [InlineData(10, 0.05)]  // 10-19 unidades -> 5%
    [InlineData(19, 0.05)]
    [InlineData(20, 0.10)]  // 20+ unidades -> 10%
    public void DescuentoPorVolumen_AplicaSegunCantidad(int quantity, double expectedRate)
    {
        var lines = new[] { new OrderLineInput(1, "Producto A", quantity, 1_000m) };
        var context = new DiscountContext(lines, CustomerHistoricalTotal: 0, Coupon: null);

        var result = _engine.Calculate(context);

        decimal expectedDiscount = Math.Round(quantity * 1_000m * (decimal)expectedRate, 2);
        Assert.Equal(expectedDiscount, result.Subtotal - result.SubtotalAfterVolume);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(500_000, 0.03)]
    [InlineData(2_000_000, 0.06)]
    [InlineData(5_000_000, 0.10)]
    public void DescuentoPorLoyalty_AplicaSegunHistoricoDelCliente(decimal historicalTotal, double expectedRate)
    {
        var lines = new[] { new OrderLineInput(1, "Producto A", 1, 100_000m) };
        var context = new DiscountContext(lines, historicalTotal, Coupon: null);

        var result = _engine.Calculate(context);

        Assert.Equal(Math.Round(100_000m * (decimal)expectedRate, 2), result.LoyaltyDiscountAmount);
    }

    [Fact]
    public void VolumenYLoyalty_SeCombinanEnUnSoloPedido_LoyaltySobreSubtotalYaConVolumen()
    {
        // 20 unidades x 10.000 = 200.000 subtotal -> 10% volumen = 20.000 -> queda 180.000
        // Cliente Gold (6%) sobre 180.000 = 10.800
        var lines = new[] { new OrderLineInput(1, "Producto A", 20, 10_000m) };
        var context = new DiscountContext(lines, CustomerHistoricalTotal: 2_000_000m, Coupon: null);

        var result = _engine.Calculate(context);

        Assert.Equal(200_000m, result.Subtotal);
        Assert.Equal(20_000m, result.Subtotal - result.SubtotalAfterVolume);
        Assert.Equal(180_000m, result.SubtotalAfterVolume);
        Assert.Equal(10_800m, result.LoyaltyDiscountAmount);
        Assert.Equal(169_200m, result.TotalFinal);
        Assert.Equal(2, result.AppliedDiscounts.Count);
    }

    [Fact]
    public void CuponStackable_SeSumaEncimaDeVolumenYLoyalty()
    {
        var lines = new[] { new OrderLineInput(1, "Producto A", 1, 1_000_000m) };
        var coupon = MakeCoupon("PROMO10", DiscountType.Percent, 10, stackable: true);
        var context = new DiscountContext(lines, CustomerHistoricalTotal: 0, coupon);

        var result = _engine.Calculate(context);

        // Sin volumen (1 unidad) ni loyalty (histórico 0) -> el cupón aplica sobre el subtotal completo
        Assert.Equal(100_000m, result.CouponDiscountAmount);
        Assert.Equal(900_000m, result.TotalFinal);
    }

    [Fact]
    public void CuponNoStackable_GanaElCuponSiEsMasBeneficiosoQueVolumenMasLoyalty()
    {
        // Subtotal 1.000.000, sin volumen ni loyalty (cliente nuevo, 1 unidad) -> combo = 1.000.000
        // Cupón fijo de 200.000 no combinable -> gana el cupón (paga 800.000 < 1.000.000)
        var lines = new[] { new OrderLineInput(1, "Producto A", 1, 1_000_000m) };
        var coupon = MakeCoupon("FIJO200K", DiscountType.Fixed, 200_000, stackable: false);
        var context = new DiscountContext(lines, CustomerHistoricalTotal: 0, coupon);

        var result = _engine.Calculate(context);

        Assert.Equal(800_000m, result.TotalFinal);
        Assert.Contains(result.AppliedDiscounts, d => d.Type == AppliedDiscountType.Coupon);
        Assert.DoesNotContain(result.AppliedDiscounts, d => d.Type == AppliedDiscountType.Volume);
    }

    [Fact]
    public void CuponNoStackable_PierdeSiVolumenMasLoyaltySonMejoresParaElCliente()
    {
        // 20 unidades x 100.000 = 2.000.000 subtotal -> 10% volumen = 200.000 -> queda 1.800.000
        // Cliente Platinum (10%) sobre 1.800.000 = 180.000 -> combo final = 1.620.000
        // Cupón fijo no combinable de solo 50.000 -> combo (1.620.000) es mucho mejor que cupón (1.950.000)
        var lines = new[] { new OrderLineInput(1, "Producto A", 20, 100_000m) };
        var coupon = MakeCoupon("CHIQUITO", DiscountType.Fixed, 50_000, stackable: false);
        var context = new DiscountContext(lines, CustomerHistoricalTotal: 6_000_000m, coupon);

        var result = _engine.Calculate(context);

        Assert.Equal(1_620_000m, result.TotalFinal);
        Assert.DoesNotContain(result.AppliedDiscounts, d => d.Type == AppliedDiscountType.Coupon);
    }

    [Fact]
    public void CuponPorcentaje_NuncaDescuentaMasQueElSubtotal()
    {
        var lines = new[] { new OrderLineInput(1, "Producto A", 1, 1_000m) };
        var coupon = MakeCoupon("MEGA200", DiscountType.Percent, 200, stackable: true); // 200% sería absurdo
        var context = new DiscountContext(lines, CustomerHistoricalTotal: 0, coupon);

        var result = _engine.Calculate(context);

        Assert.Equal(0, result.TotalFinal);
        Assert.True(result.CouponDiscountAmount <= result.Subtotal);
    }

    [Fact]
    public void PedidoSinLineas_LanzaExcepcion()
    {
        var context = new DiscountContext(Array.Empty<OrderLineInput>(), 0, null);
        Assert.Throws<ArgumentException>(() => _engine.Calculate(context));
    }
}
