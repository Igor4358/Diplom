namespace WMS.Terminal.Models
{
    public class Warehouse
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int? CityId { get; set; }         
        public int? TravelDays { get; set; }      
        public string? Address { get; set; }

        public City? City { get; set; }
        public List<Cell> Cells { get; set; } = new();
        public List<User> Users { get; set; } = new();
    }
}