using System.ComponentModel.DataAnnotations;

namespace WMS.Terminal.Models
{
    public class PackageItem
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int PackageId { get; set; }

        [Required]
        public int ProductId { get; set; }

        public int Quantity { get; set; } = 1;

        public string? Barcode { get; set; }  

        public DateTime AddedAt { get; set; } = DateTime.UtcNow;

        public Package? Package { get; set; }
        public Product? Product { get; set; }
    }
}