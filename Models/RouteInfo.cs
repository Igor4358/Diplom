namespace WMS.Terminal.Models
{
    public class RouteInfo
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string CityName { get; set; } = string.Empty;
        public int TravelDays { get; set; }
        public bool HasRoute { get; set; }
    }
}