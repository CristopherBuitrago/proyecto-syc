namespace OrdersApi.DTOs;

public record CreateOrderItemRequest(int ProductId, int Quantity);

public record CreateOrderRequest(int CustomerId, List<CreateOrderItemRequest> Items, string? CouponCode);

public record AppliedDiscountResponse(string Type, string Description, decimal Amount);

public record OrderItemResponse(int ProductId, string ProductName, int Quantity, decimal UnitPrice, decimal LineDiscount, decimal LineTotal);

public record OrderResponse(
    int Id,
    int CustomerId,
    string Status,
    decimal Subtotal,
    decimal TotalDiscount,
    decimal TotalFinal,
    DateTimeOffset CreatedAt,
    List<OrderItemResponse> Items,
    List<AppliedDiscountResponse> AppliedDiscounts
);

// Estructura de error uniforme para ambos backends (.NET y Node) — requisito no
// funcional explícito de la sección 8 del enunciado.
public record ApiError(string Message, string? Code = null, Dictionary<string, object>? Details = null);
