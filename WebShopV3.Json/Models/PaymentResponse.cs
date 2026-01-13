namespace WebShopV3.Json.Models
{
    public class PaymentResponse
    {
        public string Id { get; set; }
        public string Status { get; set; }
        public bool Paid { get; set; }
        public decimal Amount { get; set; }
        public string ConfirmationUrl { get; set; }
    }
}
