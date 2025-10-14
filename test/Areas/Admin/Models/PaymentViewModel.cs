using test.Entity;

namespace test.Areas.Admin.Models
{
    public class PaymentViewModel
    {
        public int? Id { get; set; }
        public int? TransactionID { get; set; }

        public string? Reason { get; set; }
        public decimal Amount { get; set; }

        public string? Method { get; set; }

        public string? PaidOn { get; set; }
        public int BorrowerID { get; set; }  
        public string BorrowerName { get; set; }

        public List<Penalty> Penalties { get; set; } = new List<Penalty>();
    }
}
