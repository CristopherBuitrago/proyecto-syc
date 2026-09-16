namespace OrdersApi.Services.Discounts;

// Patrón Strategy: cada regla de descuento es intercambiable e independiente.
// Agregar una regla nueva (ej. "descuento por temporada") es crear una clase que
// implemente esta interfaz y registrarla en DiscountEngine — sin tocar las demás
// reglas ni el orquestador. Ver DECISIONS.md, pregunta sobre extensibilidad futura.
public interface IVolumeDiscountRule
{
    // Se evalúa por línea de producto (el descuento por volumen es "a nivel de línea").
    LineDiscountResult ApplyToLine(OrderLineInput line);
}

public interface ILoyaltyDiscountRule
{
    // Se evalúa una sola vez por pedido, sobre el subtotal ya con volumen aplicado.
    decimal CalculateDiscount(decimal subtotalAfterVolume, decimal customerHistoricalTotal);
    string DescribeTier(decimal customerHistoricalTotal);
}
