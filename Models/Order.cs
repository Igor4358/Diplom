using System.ComponentModel.DataAnnotations;

namespace WMS.Terminal.Models
{
    public class Order
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string OrderNumber { get; set; } = string.Empty;  // "126083522"

        [Required]
        [MaxLength(20)]
        public string Status { get; set; } = "New";  // New, Picking, Done

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Навигационные свойства
        public List<OrderItem> OrderItems { get; set; } = new();
        public List<PickingTask> PickingTasks { get; set; } = new();
    }
}