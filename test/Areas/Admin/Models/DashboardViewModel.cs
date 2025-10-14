namespace test.Areas.Admin.Models
{
    public class DashboardViewModel
    {
        public int TotalBooks { get; set; }
        public int TotalBorrowers { get; set; }
        public int TotalTransactions { get; set; }
        public int TotalOverdue { get; set; }

        public List<object> BooksBorrowedPerDay { get; set; } = new();
        public List<object> BooksBorrowedPerWeek { get; set; } = new();
        public List<object> BooksBorrowedPerMonth { get; set; } = new();

        public List<object> TopBorrowedBooks { get; set; } = new();
    }
}
