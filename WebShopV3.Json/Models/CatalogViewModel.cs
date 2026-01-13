namespace WebShopV3.Json.Models
{
    public class CatalogViewModel
    {
        public List<Computer> Computers { get; set; } = new List<Computer>();
        public List<Component> Components { get; set; } = new List<Component>();
        public string ProductType { get; set; } = "all"; // all, Computers, Components
        public int TotalCount => Computers.Count + Components.Count;
    }
}
