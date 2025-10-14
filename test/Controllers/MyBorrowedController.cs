using test.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using test.Models;
using X.PagedList.Extensions;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;


namespace test.Controllers
{
    [Authorize(Roles = "Borrower")]
    public class MyBorrowedController : Controller
    {
        private readonly ApplicationDbContext _context;
        public MyBorrowedController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public IActionResult Index(string searchQuery, int? page)
        {
            int pageSize = 9;
            int pageNumber = page ?? 1;

            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int userId))
            {
                return Unauthorized();
            }

            var transactions = _context.Transactions
                .Where(t => t.BorrowerID == userId)
                .Include(t => t.BookCopy)
                    .ThenInclude(bc => bc.Book)
                        .ThenInclude(b => b.Author)
                .Select(t => new TransactionViewModel
                {
                    Id = t.Id,
                    BookCopyID = t.BookCopy.Id,
                    TransactionDate = t.TransactionDate,
                    DueDate = t.DueDate,
                    ReturnDate = t.ReturnDate,
                    Status = t.Status,
                    BookCopy = new BookCopyViewModel
                    {
                        CopyNumber = t.BookCopy.CopyNumber,
                        Status = t.BookCopy.Status,
                        Book = new BookViewModel
                        {
                            Title = t.BookCopy.Book.Title,
                            CoverImage = t.BookCopy.Book.CoverImage,
                            AuthorName = t.BookCopy.Book.Author != null ? t.BookCopy.Book.Author.Name : "Unknown Author"
                        }
                    }
                })
                .ToList();

            if (!string.IsNullOrEmpty(searchQuery))
            {
                transactions = transactions.Where(t =>
                    t.BookCopy.Book.Title.Contains(searchQuery, StringComparison.OrdinalIgnoreCase) ||
                    t.BookCopy.Book.AuthorName.Contains(searchQuery, StringComparison.OrdinalIgnoreCase))
                .ToList();
            }

            var pagedList = transactions.ToPagedList(pageNumber, pageSize);
            return View(pagedList);
        }

        [HttpGet]
        public IActionResult Details(int id)
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int userId))
            {
                return Unauthorized();
            }

            var transaction = _context.Transactions
                .Where(t => t.Id == id && t.BorrowerID == userId)
                .Include(t => t.BookCopy)
                    .ThenInclude(bc => bc.Book)
                        .ThenInclude(b => b.Author)
                .Include(t => t.Penalty)
                .Select(t => new TransactionViewModel
                {
                    Id = t.Id,
                    TransactionDate = t.TransactionDate,
                    DueDate = t.DueDate,
                    ReturnDate = t.ReturnDate,
                    Status = t.Status,
                    BookCopy = new BookCopyViewModel
                    {
                        CopyNumber = t.BookCopy.CopyNumber,
                        Status = t.BookCopy.Status,
                        Book = new BookViewModel
                        {
                            Title = t.BookCopy.Book.Title,
                            AuthorName = t.BookCopy.Book.Author != null ? t.BookCopy.Book.Author.Name : "Unknown Author",
                            CoverImage = t.BookCopy.Book.CoverImage
                        }
                    },
                    Penalty = t.Penalty != null
                        ? t.Penalty.Select(p => new PenaltyViewModel
                        {
                            Reason = p.Reason,
                            Amount = p.Amount,
                            IsPaid = p.IsPaid
                        }).ToList()
                        : new List<PenaltyViewModel>()
                })
                .FirstOrDefault();

            if (transaction == null)
            {
                return NotFound();
            }

            return View(transaction);
        }

        [HttpGet]
        public IActionResult Overdue(string searchQuery, int? page)
        {
            int pageSize = 9;
            int pageNumber = page ?? 1;

            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int userId))
            {
                return Unauthorized();
            }

            var overdueQuery = _context.Overdues
                .Include(o => o.Transaction)
                    .ThenInclude(t => t.User)
                .Include(o => o.Transaction)
                    .ThenInclude(t => t.BookCopy)
                        .ThenInclude(bc => bc.Book)
                            .ThenInclude(b => b.Author)
                .Where(o => o.Transaction.BorrowerID == userId &&
                            o.Transaction.DueDate < DateTime.Now &&
                            o.Transaction.ReturnDate == null &&
                            o.Transaction.Status == "Overdue"); 


        var overdueList = overdueQuery
            .Select(o => new OverdueViewModel
            {
                Id = o.Id,
                TransactionID = o.TransactionID,
                BorrowerName = o.Transaction.User.Name,
                BookTitle = o.Transaction.BookCopy.Book.Title,
                BookCover = o.Transaction.BookCopy.Book.CoverImage,
                Author = o.Transaction.BookCopy.Book.Author != null
                     ? o.Transaction.BookCopy.Book.Author.Name
                     : "Unknown Author",
                CopyNumber = o.Transaction.BookCopy.CopyNumber,
                DueDate = o.Transaction.DueDate,
                OverdueDays = o.OverdueDays,
                FineAmount = o.Transaction.Penalty
                    .Where(p => p.Reason == "Overdue")
                    .Select(p => p.Amount)
                    .FirstOrDefault(),
                TransactionStatus = o.Transaction.Status
            })
            .ToPagedList(pageNumber, pageSize);
        return View(overdueList);
        }

        [HttpGet]
        [HttpGet]
        public IActionResult OverdueDetails(int id)
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int userId))
            {
                return Unauthorized();
            }

            var overdueTransaction = _context.Overdues
                .Include(o => o.Transaction)
                    .ThenInclude(t => t.User)
                .Include(o => o.Transaction)
                    .ThenInclude(t => t.BookCopy)
                        .ThenInclude(bc => bc.Book)
                            .ThenInclude(b => b.Author)
                .Include(o => o.Transaction)
                    .ThenInclude(t => t.Penalty)
                .Where(o => o.Id == id && o.Transaction.BorrowerID == userId)
                .Select(o => new OverdueViewModel
                {
                    Id = o.Id,
                    OverdueDays = o.OverdueDays,
                    FineAmount = o.Transaction.Penalty
                        .Where(p => p.Reason == "Overdue")
                        .Select(p => p.Amount)
                        .FirstOrDefault(),

                    Transaction = new TransactionViewModel
                    {
                        Id = o.Transaction.Id,
                        TransactionDate = o.Transaction.TransactionDate,
                        DueDate = o.Transaction.DueDate,
                        ReturnDate = o.Transaction.ReturnDate,
                        Status = o.Transaction.Status,
                        BorrowerName = o.Transaction.User.Name,

                        BookCopy = new BookCopyViewModel
                        {
                            CopyNumber = o.Transaction.BookCopy.CopyNumber,
                            Status = o.Transaction.BookCopy.Status,
                            Book = new BookViewModel
                            {
                                Title = o.Transaction.BookCopy.Book.Title,
                                CoverImage = o.Transaction.BookCopy.Book.CoverImage,
                                AuthorName = o.Transaction.BookCopy.Book.Author != null
                                    ? o.Transaction.BookCopy.Book.Author.Name
                                    : "Unknown Author"
                            }
                        },
                        Penalty = o.Transaction.Penalty != null
                            ? o.Transaction.Penalty.Select(p => new PenaltyViewModel
                            {
                                Reason = p.Reason,
                                Amount = p.Amount,
                                IsPaid = p.IsPaid
                            }).ToList()
                            : new List<PenaltyViewModel>()
                    }
                })
                .FirstOrDefault();

            if (overdueTransaction == null)
            {
                return NotFound();
            }

            return View(overdueTransaction);
        }


    }
}
