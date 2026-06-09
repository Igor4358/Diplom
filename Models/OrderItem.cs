using System.ComponentModel.DataAnnotations;

namespace WMS.Terminal.Models
{
    public class OrderItem
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int OrderId { get; set; }

        [Required]
        public int ProductId { get; set; }

        [Required]
        public int Quantity { get; set; }  // Сколько заказано

        public int PickedQuantity { get; set; } = 0;  // Сколько уже собрано

        // Навигационные свойства
        public Order? Order { get; set; }
        public Product? Product { get; set; }
    }
}