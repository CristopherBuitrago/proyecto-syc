namespace OrdersApi.Services;

// Excepciones de dominio, cada una mapea a un código HTTP específico en el controller.
// Mensajes claros porque el enunciado los pide explícitamente (sección 3).
public class CustomerNotFoundException : Exception
{
    public CustomerNotFoundException(int customerId) : base($"No existe un cliente con id {customerId}.") { }
}

public class ProductNotFoundException : Exception
{
    public ProductNotFoundException(int productId) : base($"No existe un producto con id {productId}.") { }
}

public class CouponInvalidException : Exception
{
    public CouponInvalidException(string message) : base(message) { }
}

// Validación de forma del pedido (cantidades, lista vacía) — se lanza ANTES de tocar
// la base de datos. Sin esto, una cantidad <= 0 llegaba hasta el CHECK (quantity > 0)
// de la tabla order_items y explotaba como una excepción no controlada de EF Core.
public class InvalidOrderException : Exception
{
    public InvalidOrderException(string message) : base(message) { }
}

public class OrderNotFoundException : Exception
{
    public OrderNotFoundException(int orderId) : base($"No existe un pedido con id {orderId}.") { }
}

public class OrderAlreadyCancelledException : Exception
{
    public OrderAlreadyCancelledException(int orderId) : base($"El pedido {orderId} ya estaba cancelado.") { }
}
