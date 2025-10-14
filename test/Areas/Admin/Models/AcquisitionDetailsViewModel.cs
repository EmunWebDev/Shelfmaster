using System.Collections.Generic;
using test.Entity;

namespace test.Areas.Admin.Models
{
    public class AcquisitionDetailsViewModel
    {
        public Acquisition Acquisition { get; set; }
        public List<AcquisitionPayment> AcquisitionPayments { get; set; }
    }
}
