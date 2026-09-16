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

public class OrderNotFoundException : Exception
{
    public OrderNotFoundException(int orderId) : base($"No existe un pedido con id {orderId}.") { }
}

public class OrderAlreadyCancelledException : Exception
{
    public OrderAlreadyCancelledException(int orderId) : base($"El pedido {orderId} ya estaba cancelado.") { }
}
