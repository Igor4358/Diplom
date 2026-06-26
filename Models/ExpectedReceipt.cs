using System.ComponentModel.DataAnnotations;

namespace WMS.Terminal.Models
{
    public class ExpectedReceipt
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Barcode { get; set; } = string.Empty;  // Уникальный штрих-код товара

        [Required]
        public string Sku { get; set; } = string.Empty;      // Артикул товара

        [Required]
        public string ProductName { get; set; } = string.Empty; // Название товара

        public int ExpectedQuantity { get; set; } = 1;       // Ожидаемое количество

        public int ReceivedQuantity { get; set; } = 0;       // Уже принято

        public string Status { get; set; } = "Pending";      // Pending, Partial, Completed

        public DateTime ExpectedDate { get; set; } = DateTime.UtcNow;

        public string? Supplier { get; set; }                // Поставщик (опционально)
    }
}