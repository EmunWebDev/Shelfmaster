using test.Entity;

namespace test.Areas.Admin.Models
{
    public class AcquisitionPaymentViewModel
    {
        public Payment NewPayment { get; set; }
        public List<Payment> ExistingPayments { get; set; }
        public string VendorName { get; set; }
        public int AcquisitionId { get; set; }
    }
}

