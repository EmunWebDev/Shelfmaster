using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using test.Data;
using test.Areas.Admin.Models;

namespace test.Areas.Admin.Controllers
{
    [Authorize]
    [Area("Admin")]
    public class DashboardController : Controller
    {
        private readonly ApplicationDbContext _context;

        public DashboardController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var model = new DashboardViewModel();

            // Totals
            model.TotalBooks = await _context.Books.CountAsync();
            model.TotalBorrowers = await _context.Users
                .Where(u => u.Role == "Borrower")
                .CountAsync();
            model.TotalTransactions = await _context.Transactions.CountAsync();
            model.TotalOverdue = await _context.Overdues
                .Where(o => o.Transaction.Status == "Overdue")
                .CountAsync();

            // --- Daily (last 7 days)
            var startDateDay = DateTime.Today.AddDays(-6);
            var endDate = DateTime.Today.AddDays(1).AddTicks(-1);

            model.BooksBorrowedPerDay = await _context.Transactions
                .Include(t => t.BookCopy).ThenInclude(bc => bc.Book)
                .Where(t => (t.Status == "Completed" || t.Status == "Overdue")
                         && t.TransactionDate >= startDateDay
                         && t.TransactionDate <= endDate)
                .GroupBy(t => t.TransactionDate.Date)
                .Select(g => new
                {
                    Date = g.Key,
                    TotalBorrowed = g.Count()
                })
                .OrderBy(g => g.Date)
                .Cast<object>()
                .ToListAsync();

            // --- Weekly (last 4 weeks) - Fixed approach
            var startDateWeek = DateTime.Today.AddDays(-27);

            // Get the raw transaction data first, then process on client side
            var weeklyTransactions = await _context.Transactions
                .Include(t => t.BookCopy).ThenInclude(bc => bc.Book)
                .Where(t => (t.Status == "Completed" || t.Status == "Overdue")
                         && t.TransactionDate >= startDateWeek
                         && t.TransactionDate <= endDate)
                .Select(t => new { t.TransactionDate })
                .ToListAsync();

            // Now group by week on the client side
            model.BooksBorrowedPerWeek = weeklyTransactions
                .GroupBy(t => new
                {
                    Year = t.TransactionDate.Year,
                    Week = CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(
                        t.TransactionDate,
                        CalendarWeekRule.FirstFourDayWeek,
                        DayOfWeek.Monday)
                })
                .Select(g => new
                {
                    g.Key.Year,
                    g.Key.Week,
                    TotalBorrowed = g.Count()
                })
                .OrderBy(g => g.Year).ThenBy(g => g.Week)
                .Cast<object>()
                .ToList();

            // --- Monthly (all time)
            model.BooksBorrowedPerMonth = await _context.Transactions
                .Include(t => t.BookCopy).ThenInclude(bc => bc.Book)
                .Where(t => t.Status == "Completed" || t.Status == "Overdue")
                .GroupBy(t => new { t.TransactionDate.Year, t.TransactionDate.Month })
                .Select(g => new
                {
                    Year = g.Key.Year,
                    Month = g.Key.Month,
                    TotalBorrowed = g.Count()
                })
                .OrderBy(g => g.Year).ThenBy(g => g.Month)
                .Cast<object>()
                .ToListAsync();

            // --- Top Borrowed Books
            model.TopBorrowedBooks = await _context.Transactions
                .Where(t => t.BookCopy != null && t.BookCopy.Book != null)
                .Include(t => t.BookCopy).ThenInclude(bc => bc.Book)
                .GroupBy(t => t.BookCopy.Book.Title)
                .Select(g => new
                {
                    BookTitle = g.Key,
                    BorrowCount = g.Count()
                })
                .OrderByDescending(g => g.BorrowCount)
                .Take(3)
                .Cast<object>()
                .ToListAsync();

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> GetBorrowedBooks(string range = "d")
        {
            var query = _context.Transactions
                .Where(t => t.Status == "Completed" || t.Status == "Overdue");

            var list = await query.ToListAsync();

            List<object> data = new List<object>();

            switch (range)
            {
                case "d": // Days of week (Mon–Fri)
                    var daysOfWeek = new[] { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday" };
                    data = daysOfWeek
                        .Select(day => new {
                            Label = day,
                            Total = list.Count(t => t.TransactionDate.ToString("dddd") == day)
                        })
                        .ToList<object>();
                    break;

                case "w": // Weeks of current month
                    var now = DateTime.Today;
                    var firstDayOfMonth = new DateTime(now.Year, now.Month, 1);
                    var weeksInMonth = Enumerable.Range(1, 5).Select(i => $"W{i}").ToList();

                    data = weeksInMonth.Select((w, i) => {
                        var start = firstDayOfMonth.AddDays(i * 7);
                        var end = start.AddDays(6);
                        return new
                        {
                            Label = w,
                            Total = list.Count(t => t.TransactionDate >= start && t.TransactionDate <= end)
                        };
                    }).ToList<object>();
                    break;

                case "m": // Months (Jan–Dec)
                    data = Enumerable.Range(1, 12)
                        .Select(m => new {
                            Label = new DateTime(2000, m, 1).ToString("MMMM"),
                            Total = list.Count(t => t.TransactionDate.Month == m)
                        })
                        .ToList<object>();
                    break;

                case "y": // Years (all distinct years)
                    data = list
                        .GroupBy(t => t.TransactionDate.Year)
                        .Select(g => new { Label = g.Key.ToString(), Total = g.Count() })
                        .OrderBy(x => x.Label)
                        .ToList<object>();
                    break;

                default:
                    data = new List<object>();
                    break;
            }

            return Json(data);
        }




        [HttpGet]
        public async Task<IActionResult> GetBorrowedByRange(DateTime start, DateTime end)
        {
            var data = await _context.Transactions
                .Where(t => (t.Status == "Completed" || t.Status == "Overdue")
                         && t.TransactionDate >= start
                         && t.TransactionDate <= end)
                .GroupBy(t => t.TransactionDate.Date)
                .Select(g => new { Label = g.Key, Total = g.Count() })
                .OrderBy(g => g.Label)
                .ToListAsync();

            return Json(data);
        }

    }
}