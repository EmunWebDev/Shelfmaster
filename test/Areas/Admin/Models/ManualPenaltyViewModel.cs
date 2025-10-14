using System.ComponentModel.DataAnnotations;

namespace test.Areas.Admin.Models
{
    public class ManualPenaltyViewModel
    {
        public int TransactionId { get; set; }

        [Required]
        public string Reason { get; set; } = "Lost";

        public string BorrowerName { get; set; } = string.Empty;
        public string BookTitle { get; set; } = string.Empty;

        public string? ISBN { get; set; }

        // Suggested value from market call (nullable)
        public decimal? SuggestedAmount { get; set; }

        [Required(ErrorMessage = "Please enter a penalty amount.")]
        [Range(1, double.MaxValue, ErrorMessage = "Amount must be greater than 0.")]
        public decimal Amount { get; set; }
    }
}
