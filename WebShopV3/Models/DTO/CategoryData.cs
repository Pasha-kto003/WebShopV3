namespace WebShopV3.Models.DTO
{
    public class CategoryData
    {
        public string Type { get; set; }
        public ComponentData Component { get; set; }
    }

    public class ComponentData
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public decimal Price { get; set; }
        public int Quantity { get; set; }
        public string Type { get; set; }
    }
}