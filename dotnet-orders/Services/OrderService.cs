using Microsoft.EntityFrameworkCore;
using OrdersApi.Data;
using OrdersApi.DTOs;
using OrdersApi.Models;
using OrdersApi.Services.Discounts;

namespace OrdersApi.Services;

public class OrderService
{
    private readonly OrdersDbContext _db;
    private readonly DiscountEngine _discountEngine;
    private readonly CatalogClient _catalogClient;
    private readonly ILogger<OrderService> _logger;

    public OrderService(OrdersDbContext db, DiscountEngine discountEngine, CatalogClient catalogClient, ILogger<OrderService> logger)
    {
        _db = db;
        _discountEngine = discountEngine;
        _catalogClient = catalogClient;
        _logger = logger;
    }

    public async Task<OrderResponse> CreateOrderAsync(CreateOrderRequest request)
    {
        var customer = await _db.Customers.FindAsync(request.CustomerId)
            ?? throw new CustomerNotFoundException(request.CustomerId);

        var productIds = request.Items.Select(i => i.ProductId).ToList();
        var products = await _db.Products.Where(p => productIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id);
        foreach (var item in request.Items)
            if (!products.ContainsKey(item.ProductId))
                throw new ProductNotFoundException(item.ProductId);

        var lines = request.Items
            .Select(i => new OrderLineInput(i.ProductId, products[i.ProductId].Name, i.Quantity, products[i.ProductId].Price))
            .ToList();

        // Histórico de compras confirmadas del cliente, calculado en vivo (no un campo
        // "tier" guardado) — así siempre refleja el estado real al momento del pedido.
        // Ver DECISIONS.md, pregunta sobre consistencia del tier tras compras seguidas.
        decimal historicalTotal = await _db.Orders
            .Where(o => o.CustomerId == request.CustomerId && o.Status == OrderStatus.Confirmed)
            .SumAsync(o => (decimal?)o.TotalFinal) ?? 0m;

        Coupon? coupon = null;
        if (!string.IsNullOrWhiteSpace(request.CouponCode))
        {
            coupon = await _db.Coupons.FirstOrDefaultAsync(c => c.Code == request.CouponCode)
                ?? throw new CouponInvalidException($"El cupón '{request.CouponCode}' no existe.");

            if (coupon.ExpiresAt < DateTimeOffset.UtcNow)
                throw new CouponInvalidException($"El cupón '{request.CouponCode}' está expirado.");

            decimal rawSubtotal = lines.Sum(l => l.LineSubtotal);
            if (rawSubtotal < coupon.MinPurchaseAmount)
                throw new CouponInvalidException(
                    $"El cupón '{request.CouponCode}' requiere una compra mínima de {coupon.MinPurchaseAmount:C0}.");
        }

        var discountResult = _discountEngine.Calculate(new DiscountContext(lines, historicalTotal, coupon));

        // Reserva el stock ANTES de persistir el pedido — si falla, no se crea nada.
        var reserveItems = request.Items.Select(i => new ReserveItem(i.ProductId, i.Quantity)).ToList();
        try
        {
            await _catalogClient.ReserveAsync(reserveItems);
        }
        catch (StockUnavailableException ex)
        {
            _logger.LogWarning("Stock insuficiente al crear pedido para cliente {CustomerId}: {Failures}",
                request.CustomerId, string.Join(", ", ex.Failures.Select(f => $"producto {f.ProductId} (pidió {f.Requested}, hay {f.Available})")));
            throw;
        }

        await using var transaction = await _db.Database.BeginTransactionAsync();
        try
        {
            var order = new Order
            {
                CustomerId = request.CustomerId,
                Status = OrderStatus.Confirmed,
                CouponCode = request.CouponCode,
                Subtotal = discountResult.Subtotal,
                TotalDiscount = discountResult.TotalDiscount,
                TotalFinal = discountResult.TotalFinal,
                CreatedAt = DateTimeOffset.UtcNow,
            };

            foreach (var line in discountResult.LineResults)
            {
                order.Items.Add(new OrderItem
                {
                    ProductId = line.ProductId,
                    Quantity = request.Items.First(i => i.ProductId == line.ProductId).Quantity,
                    UnitPrice = products[line.ProductId].Price,
                    LineDiscount = line.VolumeDiscountAmount,
                    LineTotal = line.LineTotalAfterVolume,
                });
            }

            foreach (var applied in discountResult.AppliedDiscounts)
            {
                order.AppliedDiscounts.Add(new AppliedDiscount
                {
                    DiscountType = applied.Type,
                    Description = applied.Description,
                    Amount = applied.Amount,
                });
            }

            _db.Orders.Add(order);
            await _db.SaveChangesAsync();
            await transaction.CommitAsync();

            return await GetOrderResponseAsync(order.Id, products);
        }
        catch
        {
            await transaction.RollbackAsync();
            // Si falló guardar el pedido después de reservar el stock, liberamos lo reservado
            // para no dejar inventario fantasma bloqueado.
            await _catalogClient.ReleaseAsync(reserveItems);
            throw;
        }
    }

    public async Task<OrderResponse> GetByIdAsync(int orderId)
    {
        var products = await _db.Products.ToDictionaryAsync(p => p.Id);
        return await GetOrderResponseAsync(orderId, products);
    }

    public async Task<List<OrderResponse>> ListAsync(int? customerId, string? status, DateTimeOffset? from, DateTimeOffset? to, int page, int pageSize)
    {
        var query = _db.Orders.Include(o => o.Items).Include(o => o.AppliedDiscounts).AsQueryable();

        if (customerId is not null) query = query.Where(o => o.CustomerId == customerId);
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<OrderStatus>(status, true, out var parsedStatus))
            query = query.Where(o => o.Status == parsedStatus);
        if (from is not null) query = query.Where(o => o.CreatedAt >= from);
        if (to is not null) query = query.Where(o => o.CreatedAt <= to);

        var orders = await query
            .OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var products = await _db.Products.ToDictionaryAsync(p => p.Id);
        return orders.Select(o => MapToResponse(o, products)).ToList();
    }

    public async Task<OrderResponse> CancelAsync(int orderId)
    {
        var order = await _db.Orders.Include(o => o.Items).Include(o => o.AppliedDiscounts)
            .FirstOrDefaultAsync(o => o.Id == orderId)
            ?? throw new OrderNotFoundException(orderId);

        if (order.Status == OrderStatus.Cancelled)
            throw new OrderAlreadyCancelledException(orderId);

        order.Status = OrderStatus.Cancelled;
        await _db.SaveChangesAsync();

        var releaseItems = order.Items.Select(i => new ReserveItem(i.ProductId, i.Quantity)).ToList();
        await _catalogClient.ReleaseAsync(releaseItems);

        var products = await _db.Products.ToDictionaryAsync(p => p.Id);
        return MapToResponse(order, products);
    }

    private async Task<OrderResponse> GetOrderResponseAsync(int orderId, Dictionary<int, Product> products)
    {
        var order = await _db.Orders.Include(o => o.Items).Include(o => o.AppliedDiscounts)
            .FirstOrDefaultAsync(o => o.Id == orderId)
            ?? throw new OrderNotFoundException(orderId);

        return MapToResponse(order, products);
    }

    private static OrderResponse MapToResponse(Order order, Dictionary<int, Product> products) => new(
        order.Id,
        order.CustomerId,
        order.Status.ToString().ToUpperInvariant(),
        order.Subtotal,
        order.TotalDiscount,
        order.TotalFinal,
        order.CreatedAt,
        order.Items.Select(i => new OrderItemResponse(
            i.ProductId,
            products.TryGetValue(i.ProductId, out var p) ? p.Name : $"Producto {i.ProductId}",
            i.Quantity, i.UnitPrice, i.LineDiscount, i.LineTotal)).ToList(),
        order.AppliedDiscounts.Select(d => new AppliedDiscountResponse(d.DiscountType.ToString().ToUpperInvariant(), d.Description, d.Amount)).ToList()
    );
}
