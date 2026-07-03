using System.ComponentModel.DataAnnotations;
namespace WMS.Terminal.Models
{
    public class UserWarehouseAccess
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }

        [Required]
        public int WarehouseId { get; set; }

        public User? User { get; set; }

        public Warehouse? Warehouse { get; set; }
    }
}
