namespace OrdersApi.Models;

// Espejo de solo lectura del catálogo — la fuente de verdad del stock vive en el
// servicio Node (Catalog & Inventory). Este registro se usa para mostrar nombre/precio
// en el desglose del pedido sin tener que llamar al otro servicio en cada consulta.
public class Product
{
    public int Id { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Stock { get; set; }
}
