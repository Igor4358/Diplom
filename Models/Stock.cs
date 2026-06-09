namespace WMS.Terminal.Models
{
    public class Stock
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public int CellId { get; set; }
        public int Quantity { get; set; }  

        // Связи
        public Product? Product { get; set; }
        public Cell? Cell { get; set; }
    }
}