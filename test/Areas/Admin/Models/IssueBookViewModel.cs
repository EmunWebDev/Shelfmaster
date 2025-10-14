namespace test.Areas.Admin.Models
{
    public class IssueBookViewModel
    {
        public int BorrowerId { get; set; }
        public List<int>? BookIds { get; set; }
        public DateTime DueDate { get; set; }
    }
}
