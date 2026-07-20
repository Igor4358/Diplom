using System.ComponentModel.DataAnnotations;

namespace WMS.Terminal.Models
{
    public class ExpectedReceipt
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Barcode { get; set; } = string.Empty;

        [Required]
        public string Sku { get; set; } = string.Empty;

        [Required]
        public string ProductName { get; set; } = string.Empty;

        public int ExpectedQuantity { get; set; } = 1;

        public int ReceivedQuantity { get; set; } = 0;

        public string Status { get; set; } = "Pending"; 

        public DateTime ExpectedDate { get; set; } = DateTime.UtcNow;

        public string? Supplier { get; set; }
        public bool IsPackage { get; set; } = false;
        public int? PackageId { get; set; }
        public string? Notes { get; set; }

        public int? WarehouseId { get; set; }

        public DateTime? CompletedAt { get; set; }

        public Warehouse? Warehouse { get; set; } 
    }
}