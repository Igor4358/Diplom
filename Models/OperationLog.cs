using System.ComponentModel.DataAnnotations;

namespace WMS.Terminal.Models
{
    public class OperationLog
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }

        [Required]
        [MaxLength(100)]
        public string UserName { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string OperationType { get; set; } = string.Empty;

        public int? WarehouseId { get; set; }
        public string Details { get; set; } = string.Empty;
        public string? Barcode { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}