using System.ComponentModel.DataAnnotations;

namespace WMS.Terminal.Models
{
    public class Package
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Barcode { get; set; } = string.Empty;  

        public string? Name { get; set; }  

        public int? CellId { get; set; }  

        public int? WarehouseId { get; set; }  

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public string? CreatedBy { get; set; }  

        public Cell? Cell { get; set; }
        public Warehouse? Warehouse { get; set; }
        public List<PackageItem> Items { get; set; } = new();
    }
}