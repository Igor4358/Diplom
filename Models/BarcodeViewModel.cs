namespace WMS.Terminal.Models
{
    public class BarcodeViewModel
    {
        public List<BarcodeProduct> Products { get; set; } = new();
        public List<BarcodeCell> Cells { get; set; } = new();
    }

    public class BarcodeProduct
    {
        public string Name { get; set; } = string.Empty;
        public string Sku { get; set; } = string.Empty;
        public string CellAddress { get; set; } = string.Empty;
        public string Barcode { get; set; } = string.Empty;
    }

    public class BarcodeCell
    {
        public string Address { get; set; } = string.Empty;
        public bool IsOccupied { get; set; }
    }
}