using System.ComponentModel.DataAnnotations;

namespace WMS.Terminal.Models
{
    public class ShipmentOrder
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string OrderNumber { get; set; } = string.Empty;  

        [Required]
        public int ProductId { get; set; }  

        public int Quantity { get; set; }  

        public int CollectedQuantity { get; set; } = 0;  // Сколько уже собрано

        public string Status { get; set; } = "Pending";  // Pending, Partial, Completed, ожидаемый, частично завершён и завершён

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? CompletedAt { get; set; }

        public int? TargetWarehouseId { get; set; }  

        public string TargetWarehouseName { get; set; } = string.Empty;  

        public string? Notes { get; set; }  // Примечания

        public Product? Product { get; set; }
    }
}
