using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using EFCore.NamingConventions;
using OrdersApi.Data;
using OrdersApi.DTOs;
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
builder.Services.AddDbContext<OrdersDbContext>(options =>
    options.UseNpgsql(connectionString, npgsql => npgsql.UseQuerySplittingBehavior(QuerySplittingBehavior.SingleQuery))
        .UseSnakeCaseNamingConvention());

builder.Services.AddScoped<IVolumeDiscountRule, VolumeDiscountRule>();
builder.Services.AddScoped<ILoyaltyDiscountRule, LoyaltyDiscountRule>();
builder.Services.AddScoped<DiscountEngine>();
builder.Services.AddScoped<OrderService>();
builder.Services.AddScoped<CustomerService>();
builder.Services.AddScoped<CouponService>();

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

// Red de seguridad para cualquier excepción no capturada explícitamente en un
// controller (ej. una violación de constraint de la base de datos): sin esto, un
// error inesperado se traduce en una respuesta abrupta sin body ni headers de CORS,
// que el navegador reporta como "Failed to fetch" en vez de un error legible — rompe
// el requisito de manejo de errores consistente (sección 8 del enunciado).
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var feature = context.Features.Get<IExceptionHandlerFeature>();
        app.Logger.LogError(feature?.Error, "Error no controlado en {Path}", context.Request.Path);

        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(
            new ApiError("Ocurrió un error inesperado en el servidor.", "INTERNAL_ERROR"));
    });
});

app.UseSwagger();
app.UseSwaggerUI();

app.UseCors();
app.UseAuthorization();
app.MapControllers();

app.Run();

// Necesario si más adelante se agregan pruebas de integración con WebApplicationFactory<Program>.
public partial class Program { }
