using Microsoft.EntityFrameworkCore;
using OrdersApi.Models;

namespace OrdersApi.Data;

public class OrdersDbContext : DbContext
{
    public OrdersDbContext(DbContextOptions<OrdersDbContext> options) : base(options) { }

    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Coupon> Coupons => Set<Coupon>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<AppliedDiscount> AppliedDiscounts => Set<AppliedDiscount>();

    // Las tablas ya existen (creadas por db/init.sql). Los nombres de columna se
    // resuelven automáticamente a snake_case vía UseSnakeCaseNamingConvention()
    // (ver Program.cs) — evita mapear cada columna a mano y el error de nombres
    // PascalCase vs snake_case que eso genera.
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Customer>().ToTable("customers");
        modelBuilder.Entity<Product>().ToTable("products");
        modelBuilder.Entity<Coupon>().ToTable("coupons");
        modelBuilder.Entity<Order>().ToTable("orders");
        modelBuilder.Entity<OrderItem>().ToTable("order_items");
        modelBuilder.Entity<AppliedDiscount>().ToTable("applied_discounts");

        modelBuilder.Entity<Coupon>().Property(c => c.DiscountType)
            .HasConversion(
                v => v == DiscountType.Percent ? "PERCENT" : "FIXED",
                v => v == "PERCENT" ? DiscountType.Percent : DiscountType.Fixed);

        modelBuilder.Entity<Order>().Property(o => o.Status)
            .HasConversion(
                v => v == OrderStatus.Confirmed ? "CONFIRMED" : "CANCELLED",
                v => v == "CONFIRMED" ? OrderStatus.Confirmed : OrderStatus.Cancelled);

        modelBuilder.Entity<AppliedDiscount>().Property(a => a.DiscountType)
            .HasConversion(
                v => v.ToString().ToUpperInvariant(),
                v => Enum.Parse<AppliedDiscountType>(v, true));
    }
}
