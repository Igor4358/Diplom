namespace WMS.Terminal.Models
{
    public class Cell
    {
        public int Id { get; set; }
        public string Address { get; set; } = string.Empty;  // "406a231"
        public int WarehouseId { get; set; }
        public bool IsLocked { get; set; } = false;

        // Связи
        public Warehouse? Warehouse { get; set; }
        public List<Stock> Stocks { get; set; } = new();
    }
}