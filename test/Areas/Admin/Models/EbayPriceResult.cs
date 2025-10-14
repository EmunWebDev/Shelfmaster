namespace test.Areas.Admin.Models
{
    public class EbayPriceResult
    {
        public decimal? PriceUsd { get; set; }
        public decimal? PhpPrice { get; set; }
        public string? ItemUrl { get; set; }
        public string Source { get; set; } = "Unknown";
    }
}
