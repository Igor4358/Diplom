namespace WMS.Terminal.Models
{
    public class City
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Region { get; set; }
        public List<Warehouse> Warehouses { get; set; } = new();
    }
}
