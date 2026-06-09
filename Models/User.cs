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
        public string PinCode { get; set; } = string.Empty;  // В реальном проекте хэшируйте!

        public int CurrentWarehouseId { get; set; }

        // Навигационное свойство (связь со складом)
        public Warehouse? CurrentWarehouse { get; set; }
    }
}