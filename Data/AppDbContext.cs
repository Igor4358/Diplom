using Microsoft.EntityFrameworkCore;
using WMS.Terminal.Models;

namespace WMS.Terminal.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options) { }

        public DbSet<User> Users { get; set; }
        public DbSet<Warehouse> Warehouses { get; set; }
        public DbSet<Cell> Cells { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<Stock> Stocks { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderItem> OrderItems { get; set; }
        public DbSet<PickingTask> PickingTasks { get; set; }
        public DbSet<ShipmentOrder> ShipmentOrders { get; set; }

        public DbSet<ExpectedReceipt> ExpectedReceipts { get; set; }
        public DbSet<ExpectedShipment> ExpectedShipments { get; set; }

        public DbSet<OperationLog> OperationLogs { get; set; }
        public DbSet<ExpectedShipment> ExpectedShipment { get; set; }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Cell>()
                .HasIndex(c => c.Address)
                .IsUnique();

            modelBuilder.Entity<Stock>()
                .HasIndex(s => new { s.ProductId, s.CellId })
                .IsUnique();
        }
    }
}