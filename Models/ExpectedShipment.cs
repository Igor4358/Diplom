using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WMS.Terminal.Models
{
    public class ExpectedShipment
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string InvoiceNumber { get; set; } = string.Empty;  // Номер накладной

        [Required]
        public int ProductId { get; set; }

        [Required]
        public int ExpectedQuantity { get; set; }

        public int ReceivedQuantity { get; set; } = 0;

        public int WarehouseId { get; set; }
        public DateTime ExpectedDate { get; set; }

        [MaxLength(20)]
        public string Status { get; set; } = "Pending";  // Pending, Partial, Completed

        [ForeignKey("ProductId")]
        public Product? Product { get; set; }
    }
}