namespace WMS.Terminal.Models
{
    public class Warehouse
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;  // "406" или "257"

        // Связи
        public List<Cell> Cells { get; set; } = new();
        public List<User> Users { get; set; } = new();
    }
}