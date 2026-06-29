using System.ComponentModel.DataAnnotations;

namespace WMS.Terminal.Models
{
    public class ShipmentOrder
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string OrderNumber { get; set; } = string.Empty;  // Номер заказа, например "SO-2026-001"

        [Required]
        public int ProductId { get; set; }  // Какой товар заказан

        public int Quantity { get; set; }  // Сколько заказано

        public int CollectedQuantity { get; set; } = 0;  // Сколько уже собрано

        public string Status { get; set; } = "Pending";  // Pending, Partial, Completed

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? CompletedAt { get; set; }

        public int? TargetWarehouseId { get; set; }  // ID склада-получателя (1-1000)

        public string TargetWarehouseName { get; set; } = string.Empty;  // Название склада-получателя

        public string? Notes { get; set; }  // Примечания

        // Навигационные свойства
        public Product? Product { get; set; }
    }
}
