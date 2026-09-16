using Microsoft.EntityFrameworkCore;
using OrdersApi.Data;
using OrdersApi.Services;
using OrdersApi.Services.Discounts;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Orders & Discount Engine API", Version = "v1" });
});

// Variables de entorno para configuración — nunca credenciales hardcodeadas (sección 8).
var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("Falta configurar ConnectionStrings__Default.");
builder.Services.AddDbContext<OrdersDbContext>(options => options.UseNpgsql(connectionString));

builder.Services.AddScoped<IVolumeDiscountRule, VolumeDiscountRule>();
builder.Services.AddScoped<ILoyaltyDiscountRule, LoyaltyDiscountRule>();
builder.Services.AddScoped<DiscountEngine>();
builder.Services.AddScoped<OrderService>();

var catalogBaseUrl = builder.Configuration["CatalogService:BaseUrl"]
    ?? throw new InvalidOperationException("Falta configurar CatalogService__BaseUrl.");
builder.Services.AddHttpClient<CatalogClient>(client =>
{
    client.BaseAddress = new Uri(catalogBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(10);
});

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy => policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseCors();
app.UseAuthorization();
app.MapControllers();

app.Run();

// Necesario si más adelante se agregan pruebas de integración con WebApplicationFactory<Program>.
public partial class Program { }
