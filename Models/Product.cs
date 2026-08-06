namespace WMS.Terminal.Models
{
    public class Product
    {
        public int Id { get; set; }
        public string Sku { get; set; } = string.Empty;      
        public string Name { get; set; } = string.Empty;     
        public string Description { get; set; } = string.Empty;

        public List<Stock> Stocks { get; set; } = new();
    }
}
