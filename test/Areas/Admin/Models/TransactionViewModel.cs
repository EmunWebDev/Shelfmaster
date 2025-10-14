using test.Entity;
using System.ComponentModel.DataAnnotations;

namespace test.Areas.Admin.Models
{
    public class TransactionViewModel
    {
        public int Id { get; set; }
        public string? BorrowerName { get; set; } 
        public string? BookTitle { get; set; } 
        public string? CopyNumber { get; set; }
        public DateTime TransactionDate { get; set; }
        public DateTime DueDate { get; set; }
        public DateTime? ReturnDate { get; set; }

        [StringLength(15)]
        public string? Status { get; set; } // Borrowed, Returned, Overdue, Cancelled, Fine Paid, Settled
        public string? BookCopyStatus { get; set; }
        public decimal? Total { get; set; }
        public string ORNumber { get; set; }
        public string? PaymentStatus { get; set; }
        public ICollection<Penalty> Penalties { get; set; } = new List<Penalty>();
    }
}
