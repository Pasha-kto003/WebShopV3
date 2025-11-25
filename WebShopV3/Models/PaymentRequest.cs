namespace WebShopV3.Models
{
    public class PaymentRequest
    {
        public decimal Amount { get; set; }
        public string Description { get; set; }
        public int OrderId { get; set; }
        public string ReturnUrl { get; set; }
    }
}
