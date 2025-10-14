namespace test.Areas.Admin.Models
{
    public class OverdueViewModel
    {
        public int Id { get; set; }
        public int TransactionID { get; set; }
        public string? BorrowerName { get; set; }
        public string? BookTitle { get; set; }
        public string? CopyNumber { get; set; }
        public DateTime DueDate { get; set; }
        public int OverdueDays { get; set; }
        public decimal FineAmount { get; set; }

        public string? TransactionStatus { get; set; }
    }

}
