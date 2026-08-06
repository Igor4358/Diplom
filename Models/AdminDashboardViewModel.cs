namespace WMS.Terminal.Models
{
    public class AdminDashboardViewModel
    {
        public List<City> Cities { get; set; } = new();
        public List<Route> Routes { get; set; } = new();
        public List<Warehouse> Warehouses { get; set; } = new();
    }
}