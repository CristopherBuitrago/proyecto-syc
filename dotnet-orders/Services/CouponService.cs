using Microsoft.EntityFrameworkCore;
using OrdersApi.Data;
using OrdersApi.DTOs;
using OrdersApi.Models;

namespace OrdersApi.Services;

// Solo lectura: no hay CRUD de cupones en el alcance de la prueba (el enunciado no lo
// pide). Este endpoint existe únicamente para que el frontend le muestre al cliente
// qué cupones vigentes puede usar, en vez de un campo de texto libre "a ciegas".
public class CouponService
{
    private readonly OrdersDbContext _db;

    public CouponService(OrdersDbContext db)
    {
        _db = db;
    }

    public async Task<List<CouponResponse>> ListActiveAsync()
    {
        var now = DateTimeOffset.UtcNow;
        return await _db.Coupons
            .Where(c => c.ExpiresAt > now)
            .OrderBy(c => c.ExpiresAt)
            .Select(c => new CouponResponse(
                c.Code,
                c.DiscountType == DiscountType.Percent ? "PERCENT" : "FIXED",
                c.DiscountValue,
                c.MinPurchaseAmount,
                c.ExpiresAt,
                c.Stackable))
            .ToListAsync();
    }
}
