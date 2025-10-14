using System;

namespace test.Areas.Admin.Models
{
    public class RecommendationViewModel
    {
        public int BookId { get; set; }
        public string Title { get; set; }

        public int TotalCopies { get; set; }
        public int AvailableCopies { get; set; }
        public int LostCopies { get; set; }

        public DateTime? LastTransaction { get; set; }
        public DateTime AcquisitionDate { get; set; }

        public string VendorName { get; set; }  // 👈 Added

        public bool NeedsReacquire { get; set; }
    }

}
