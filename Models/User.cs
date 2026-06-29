using System.ComponentModel.DataAnnotations;

namespace WMS.Terminal.Models
{
    public class User
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [MaxLength(10)]
        public string PinCode { get; set; } = string.Empty;

        public int CurrentWarehouseId { get; set; }

        [MaxLength(20)]
        public string Role { get; set; } = "Worker"; // Admin или Worker

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? LastLoginAt { get; set; }

        public Warehouse? CurrentWarehouse { get; set; }
    }
}