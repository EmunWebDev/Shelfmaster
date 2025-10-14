using test.Data;
using test.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Security.Claims;

namespace test.Controllers
{
    [Authorize(Roles = "Borrower")]
    public class DashboardController : Controller
    {
        private readonly ApplicationDbContext _context;

        public DashboardController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> IndexAsync()
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int userId))
            {
                return Unauthorized();
            }

            int totalBorrowedBooks = await _context.Transactions
                .Where(t => t.BorrowerID == userId)
                .CountAsync();

            int totalOverdue = await _context.Overdues
                .Where(o => o.Transaction.Status == "Overdue" && o.Transaction.BorrowerID == userId)
                .CountAsync();

            ViewData["TotalBorrowed"] = totalBorrowedBooks;
            ViewData["TotalOverdue"] = totalOverdue;

            return View();
        }

       
        
    }
}
