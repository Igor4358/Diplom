using System.ComponentModel.DataAnnotations;

namespace WMS.Terminal.Models
{
    public class Route
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int FromWarehouseId { get; set; }

        [Required]
        public int ToWarehouseId { get; set; }

        [Required]
        public int TravelDays { get; set; }  

        public decimal? DistanceKm { get; set; }  

        public string? Description { get; set; }  // Описание 

        public Warehouse? FromWarehouse { get; set; }
        public Warehouse? ToWarehouse { get; set; }
    }
}
