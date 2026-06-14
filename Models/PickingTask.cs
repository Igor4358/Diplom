using System.ComponentModel.DataAnnotations;

namespace WMS.Terminal.Models
{
    public class PickingTask
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int OrderId { get; set; }

        [Required]
        public int ProductId { get; set; }

        [Required]
        public int CellId { get; set; }  

        [Required]
        public int RequiredQuantity { get; set; }

        public int PickedQuantity { get; set; } = 0;

        [Required]
        [MaxLength(20)]
        public string Status { get; set; } = "New";  // New, InProgress, Completed

        // Навигационные свойства
        public Order? Order { get; set; }
        public Product? Product { get; set; }
        public Cell? Cell { get; set; }
    }
}