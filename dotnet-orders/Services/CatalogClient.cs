using System.Text.Json;
using System.Text;

namespace OrdersApi.Services;

public record ReserveItem(int ProductId, int Quantity);

public record ReserveFailure(int ProductId, int Requested, int Available);

public class StockUnavailableException : Exception
{
    public List<ReserveFailure> Failures { get; }
    public StockUnavailableException(List<ReserveFailure> failures)
        : base("Stock insuficiente para uno o más productos del pedido.")
    {
        Failures = failures;
    }
}

// Cliente HTTP hacia el servicio Node (Catalog & Inventory). Toda la validación y
// reserva de stock pasa por acá — el servicio .NET nunca toca directamente el stock
// en la base de datos, respetando el límite de responsabilidad entre servicios que
// pide el enunciado (sección 3, "Consultar al servicio Node.js para validar stock").
public class CatalogClient
{
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public CatalogClient(HttpClient http)
    {
        _http = http;
    }

    public async Task ReserveAsync(IEnumerable<ReserveItem> items)
    {
        var payload = new { items = items.Select(i => new { productId = i.ProductId, quantity = i.Quantity }) };
        var response = await _http.PostAsync("/api/inventory/reserve", JsonContent(payload));

        if (response.IsSuccessStatusCode) return;

        if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
        {
            var body = await response.Content.ReadAsStringAsync();
            var failures = ParseFailures(body);
            throw new StockUnavailableException(failures);
        }

        throw new InvalidOperationException($"El servicio de catálogo respondió con error inesperado: {response.StatusCode}");
    }

    public async Task ReleaseAsync(IEnumerable<ReserveItem> items)
    {
        var payload = new { items = items.Select(i => new { productId = i.ProductId, quantity = i.Quantity }) };
        var response = await _http.PostAsync("/api/inventory/release", JsonContent(payload));
        response.EnsureSuccessStatusCode();
    }

    private static StringContent JsonContent(object payload) =>
        new(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json");

    private static List<ReserveFailure> ParseFailures(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            var failures = new List<ReserveFailure>();
            if (doc.RootElement.TryGetProperty("details", out var details) &&
                details.TryGetProperty("failedItems", out var failedItems))
            {
                foreach (var item in failedItems.EnumerateArray())
                {
                    failures.Add(new ReserveFailure(
                        item.GetProperty("productId").GetInt32(),
                        item.GetProperty("requested").GetInt32(),
                        item.GetProperty("available").GetInt32()));
                }
            }
            return failures;
        }
        catch (JsonException)
        {
            return new List<ReserveFailure>();
        }
    }
}
