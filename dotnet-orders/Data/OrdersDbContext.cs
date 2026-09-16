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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Las tablas ya existen (creadas por db/init.sql al levantar el contenedor de
        // Postgres) — acá solo mapeamos nombres en snake_case, sin usar migraciones de EF.
        modelBuilder.Entity<Customer>().ToTable("customers");
        modelBuilder.Entity<Product>().ToTable("products");
        modelBuilder.Entity<Coupon>(e =>
        {
            e.ToTable("coupons");
            e.Property(c => c.DiscountType)
                .HasConversion(
                    v => v == DiscountType.Percent ? "PERCENT" : "FIXED",
                    v => v == "PERCENT" ? DiscountType.Percent : DiscountType.Fixed);
        });
        modelBuilder.Entity<Order>(e =>
        {
            e.ToTable("orders");
            e.Property(o => o.Status)
                .HasConversion(
                    v => v == OrderStatus.Confirmed ? "CONFIRMED" : "CANCELLED",
                    v => v == "CONFIRMED" ? OrderStatus.Confirmed : OrderStatus.Cancelled);
        });
        modelBuilder.Entity<OrderItem>().ToTable("order_items");
        modelBuilder.Entity<AppliedDiscount>(e =>
        {
            e.ToTable("applied_discounts");
            e.Property(a => a.DiscountType)
                .HasConversion(
                    v => v.ToString().ToUpperInvariant(),
                    v => Enum.Parse<AppliedDiscountType>(v, true));
        });

        // snake_case en las columnas (Postgres) mapeadas a PascalCase (C#)
        modelBuilder.Entity<Customer>(e =>
        {
            e.Property(c => c.CreatedAt).HasColumnName("created_at");
        });
        modelBuilder.Entity<Order>(e =>
        {
            e.Property(o => o.CustomerId).HasColumnName("customer_id");
            e.Property(o => o.CouponCode).HasColumnName("coupon_code");
            e.Property(o => o.TotalDiscount).HasColumnName("total_discount");
            e.Property(o => o.TotalFinal).HasColumnName("total_final");
            e.Property(o => o.CreatedAt).HasColumnName("created_at");
        });
        modelBuilder.Entity<OrderItem>(e =>
        {
            e.Property(i => i.OrderId).HasColumnName("order_id");
            e.Property(i => i.ProductId).HasColumnName("product_id");
            e.Property(i => i.UnitPrice).HasColumnName("unit_price");
            e.Property(i => i.LineDiscount).HasColumnName("line_discount");
            e.Property(i => i.LineTotal).HasColumnName("line_total");
        });
        modelBuilder.Entity<AppliedDiscount>(e =>
        {
            e.Property(a => a.OrderId).HasColumnName("order_id");
            e.Property(a => a.DiscountType).HasColumnName("discount_type");
        });
        modelBuilder.Entity<Coupon>(e =>
        {
            e.Property(c => c.DiscountType).HasColumnName("discount_type");
            e.Property(c => c.DiscountValue).HasColumnName("discount_value");
            e.Property(c => c.MinPurchaseAmount).HasColumnName("min_purchase_amount");
            e.Property(c => c.ExpiresAt).HasColumnName("expires_at");
        });
    }
}
