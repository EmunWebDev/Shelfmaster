using DocumentFormat.OpenXml.Bibliography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QRCoder;
using QuestPDF.Infrastructure;
using System.Drawing;
using System.Drawing.Imaging;
using test.Areas.Admin.Models;
using test.Data;
using test.Entity;
using test.Enums;
using test.Services;
using X.PagedList.Extensions;

namespace test.Areas.Admin.Controllers
{
    [Authorize]
    [Area("Admin")]
    public class LendingController : BaseController
    {
        private readonly ApplicationDbContext _context;
        private readonly EmailService _emailService;
        private readonly LogService _logService;
        private readonly EbayBookService _ebayBookService;

        public LendingController(ApplicationDbContext context, EmailService emailService, LogService logService, EbayBookService ebayBookService)
        {
            _context = context;
            _emailService = emailService;
            _logService = logService;
            _ebayBookService = ebayBookService;
        }

        [HttpGet]
        public IActionResult TransactionsIndex(string searchQuery, string statusFilter, int? page)
        {
            int pageSize = 10;
            int pageNumber = page ?? 1;

            var transactions = _context.Transactions
                .Include(t => t.User)
                .Include(t => t.BookCopy)
                    .ThenInclude(bc => bc.Book)
                .Include(t => t.Penalty)
                .Include(t => t.Payment)
                .Include(t => t.User) //navigation to Users
                .OrderByDescending(t => t.TransactionDate)
                .AsQueryable();

            // Search filter
            if (!string.IsNullOrEmpty(searchQuery))
            {
                transactions = transactions.Where(t =>
                    t.BookCopy.Book.Title.Contains(searchQuery) ||
                    t.User.Name.Contains(searchQuery) || // 👈 match by user name
                    (t.Payment != null && t.Payment.ORNumber.Contains(searchQuery))
                );
            }

            // Status filter
            if (!string.IsNullOrEmpty(statusFilter))
            {
                transactions = transactions.Where(t => t.Status == statusFilter);
            }

            var transactionViewModels = transactions
                .Select(t => new TransactionViewModel
                {
                    Id = t.Id,
                    BookTitle = t.BookCopy.Book.Title,
                    CopyNumber = t.BookCopy.CopyNumber,
                    TransactionDate = t.TransactionDate,
                    DueDate = t.DueDate,
                    ReturnDate = t.ReturnDate,
                    Status = t.Status ?? "Unknown",
                    BookCopyStatus = t.BookCopy.Status,
                    Total = t.Penalty != null ? t.Penalty.Sum(p => (decimal?)p.Amount) ?? 0 : 0,
                    Penalties = t.Penalty != null ? t.Penalty.ToList() : new List<Penalty>(),

                    // Borrower Name — enrich from Students/Staffs if available
                    BorrowerName =
                        (from s in _context.Students
                         where s.Email == t.User.Email
                         select s.FirstName + " " + s.LastName).FirstOrDefault()
                        ??
                        (from st in _context.Staffs
                         where st.Email == t.User.Email
                         select st.FirstName + " " + st.LastName).FirstOrDefault()
                        ?? t.User.Name, // fallback to Users.Name

                    ORNumber = t.Payment != null ? t.Payment.ORNumber : null
                })
                .ToPagedList(pageNumber, pageSize);

            return View(transactionViewModels);
        }


        public IActionResult Issue()
        {
            var books = _context.Books.Select(b => new { b.Id, b.Title }).ToList();
            ViewBag.Books = books;

            return View();
        }

        [HttpGet]
        public IActionResult SearchBooks(string term)
        {
            var books = _context.Books
                .Where(b => b.Title.ToLower().Contains(term.ToLower()) &&
                            _context.BookCopies.Any(bc => bc.BookID == b.Id && bc.Status == "Available")) // Ensure at least one available copy
                .Select(b => new
                {
                    b.Id,
                    b.Title,
                    AvailableCopies = _context.BookCopies.Count(bc => bc.BookID == b.Id && bc.Status == "Available") // Count available copies
                })
                .Take(5)
                .ToList();

            return Json(books);
        }


        [HttpPost]
        public IActionResult IssueBook([FromBody] IssueBookViewModel model)
        {
            if (model == null || model.BookIds == null || !model.BookIds.Any())
            {
                return BadRequest(new { message = "Invalid data. Please select a borrower and at least one book." });
            }

            // Prevent past due dates
            if (model.DueDate < DateTime.Now.Date)
            {
                return BadRequest(new { message = "Due Date cannot be earlier than today." });
            }

            // Borrower must exist in Users
            var borrowerUser = _context.Users.FirstOrDefault(u => u.Id == model.BorrowerId && u.Status == UserStatus.Active);
            if (borrowerUser == null)
            {
                return BadRequest(new { message = "Invalid borrower. Please select a valid user." });
            }

            // Try to enrich borrower info from Students or Staffs (by email)
            var student = _context.Students.FirstOrDefault(s => s.Email == borrowerUser.Email);
            var staff = _context.Staffs.FirstOrDefault(st => st.Email == borrowerUser.Email);

            string borrowerName = student != null
                ? $"{student.FirstName} {student.LastName}"
                : staff != null
                    ? $"{staff.FirstName} {staff.LastName}"
                    : borrowerUser.Name;

            string borrowerType = student != null ? "Student" : staff != null ? "Staff" : "User";

            // Borrowing Limit Check
            int maxBorrowLimit = 3;
            int currentlyBorrowed = _context.Transactions
                .Count(t => t.BorrowerID == model.BorrowerId && t.Status == "Active" && t.ReturnDate == null);

            if (currentlyBorrowed >= maxBorrowLimit)
            {
                return BadRequest(new
                {
                    message = $"Borrowing limit reached. A borrower can only have {maxBorrowLimit} active borrowed book(s)."
                });
            }

            List<string> issuedBooks = new List<string>();

            foreach (var bookId in model.BookIds)
            {
                bool alreadyBorrowed = _context.Transactions
                    .Any(t => t.BorrowerID == model.BorrowerId &&
                              t.BookCopy.BookID == bookId &&
                              t.ReturnDate == null);

                if (alreadyBorrowed)
                {
                    return BadRequest(new { message = $"The borrower already has a borrowed copy of this book (Book ID: {bookId})." });
                }

                if (currentlyBorrowed + issuedBooks.Count + 1 > maxBorrowLimit)
                {
                    return BadRequest(new { message = "Borrowing this selection will exceed the maximum limit." });
                }

                var availableBookCopy = _context.BookCopies
                    .FirstOrDefault(bc => bc.BookID == bookId && bc.Status == "Available");

                if (availableBookCopy == null)
                {
                    return BadRequest(new { message = $"No available copies for book ID {bookId}." });
                }

                var transaction = new Transaction
                {
                    BorrowerID = borrowerUser.Id, // 👈 now Users.Id
                    BookCopyID = availableBookCopy.Id,
                    TransactionDate = DateTime.Now,
                    DueDate = model.DueDate,
                    Status = "Active"
                };

                availableBookCopy.Status = "Borrowed";
                _context.Transactions.Add(transaction);

                issuedBooks.Add($"'{availableBookCopy?.Book?.Title}' (Copy #{availableBookCopy?.CopyNumber})");
            }

            _context.SaveChanges();

            if (issuedBooks.Any())
            {
                _logService.LogAction(
                    UserId,
                    "New Transaction",
                    $"Issued {string.Join(", ", issuedBooks)} to {borrowerName} ({borrowerType}). Due date: {model.DueDate:MMMM dd, yyyy}."
                );
            }

            return Ok(new
            {
                message = $"{issuedBooks.Count} book(s) issued successfully to {borrowerName} " +
              $"(Now borrowing {currentlyBorrowed + issuedBooks.Count}/3)."
            });
        }



        [HttpGet]
        public IActionResult TransactionDetails(int transactionId)
        {
            var transaction = _context.Transactions
                .Include(t => t.User) // 👈 make sure User is loaded
                .Include(t => t.BookCopy)
                    .ThenInclude(bc => bc.Book)
                .Include(t => t.Penalty)
                .Include(t => t.Payment)
                .FirstOrDefault(t => t.Id == transactionId);

            if (transaction == null)
            {
                return NotFound();
            }

            decimal totalPenalty = transaction.Penalty?.Sum(p => (decimal?)p.Amount) ?? 0;
            decimal totalPayment = transaction.Payment?.Amount ?? 0;
            string paymentStatus = totalPayment >= totalPenalty ? "Fully Paid" : "Partially Paid";

            // 🔎 Borrower Name enrichment
            var student = _context.Students.FirstOrDefault(s => s.Email == transaction.User.Email);
            var staff = _context.Staffs.FirstOrDefault(st => st.Email == transaction.User.Email);

            string borrowerName = student != null
                ? $"{student.FirstName} {student.LastName}"
                : staff != null
                    ? $"{staff.FirstName} {staff.LastName}"
                    : transaction.User?.Name ?? "Unknown Borrower";

            var transactionViewModel = new TransactionViewModel
            {
                Id = transaction.Id,
                BookTitle = transaction.BookCopy.Book.Title,
                CopyNumber = transaction.BookCopy.CopyNumber,
                TransactionDate = transaction.TransactionDate,
                DueDate = transaction.DueDate,
                ReturnDate = transaction.ReturnDate,
                Status = transaction.Status ?? "Unknown",
                BookCopyStatus = transaction.BookCopy.Status,
                Total = totalPenalty,
                Penalties = transaction.Penalty ?? new List<Penalty>(),
                PaymentStatus = paymentStatus,
                BorrowerName = borrowerName 
            };

            return View(transactionViewModel);
        }



        [HttpPost]
        public async Task<IActionResult> Return(int id)
        {
            try
            {
                var transaction = await _context.Transactions
                    .Include(t => t.BookCopy)
                    .FirstOrDefaultAsync(t => t.Id == id);

                if (transaction == null)
                {
                    return NotFound();
                }

                if (transaction.ReturnDate.HasValue)
                {
                    return BadRequest(new { message = "The book has already been returned." });
                }

                transaction.ReturnDate = DateTime.Now;
                transaction.Status = "Completed";
                transaction.BookCopy.Status = "Available";


                await _context.SaveChangesAsync();
                TempData["Message"] = "Transaction Complete: book successfully returned!";

                _logService.LogAction(
                    UserId,
                    "Book Return",
                    $"{Username} returned '{transaction?.BookCopy?.Book?.Title}' (Copy #{transaction?.BookCopy.CopyNumber})."
                );

                TempData["messageType"] = "success";
                return RedirectToAction("TransactionsIndex");
            }
            catch (Exception ex)
            {
                TempData["message"] = "An error occurred: " + ex.Message;
                TempData["messageType"] = "error";
                return RedirectToAction("TransactionsIndex");
            }
        }

        [HttpPost]
        public async Task<IActionResult> Renew(int id)
        {
            try
            {
                var transaction = await _context.Transactions
                    .Include(t => t.User)       // Borrower (always User)
                    .Include(t => t.BookCopy)
                    .FirstOrDefaultAsync(t => t.Id == id);

                if (transaction == null) return NotFound();

                // Count lost or damaged books by the user
                var userLostAndDamagedCount = await _context.Transactions
                    .Where(t => t.BorrowerID == transaction.BorrowerID &&
                                (t.Status == "Lost" || t.Status == "Damaged"))
                    .CountAsync();

                // Check if the transaction is overdue
                if (transaction.DueDate < DateTime.Now)
                {
                    TempData["message"] = "The transaction is overdue and has been cancelled.";
                    TempData["messageType"] = "error";
                    return RedirectToAction("TransactionsIndex");
                }
                else if (userLostAndDamagedCount >= 3)
                {
                    TempData["message"] = "You have reached the maximum number of lost or damaged books. You cannot renew any more books.";
                    TempData["messageType"] = "error";
                    return RedirectToAction("TransactionsIndex");
                }

                // **Renewal Logic**: Set renewal period to 2 days (as specified)
                int renewalPeriod = 2;  // Fixed renewal period of 2 days

                // **Extend the due date for the renewal period**
                transaction.DueDate = DateTime.Now.AddDays(renewalPeriod);
                transaction.ReturnDate = null;
                transaction.Status = "Active";
                transaction.BookCopy.Status = "Borrowed";

                _context.Update(transaction);
                await _context.SaveChangesAsync();

                // 🔎 Enrich borrower info
                var student = _context.Students.FirstOrDefault(s => s.Email == transaction.User.Email);
                var staff = _context.Staffs.FirstOrDefault(st => st.Email == transaction.User.Email);

                string borrowerName = student != null
                    ? $"{student.FirstName} {student.LastName}"
                    : staff != null
                        ? $"{staff.FirstName} {staff.LastName}"
                        : transaction.User.Name;

                _logService.LogAction(
                    UserId,
                    "Book Renew",
                    $"{Username} renewed '{transaction?.BookCopy?.Book?.Title}' (Copy #{transaction?.BookCopy.CopyNumber}) for {borrowerName}."
                );

                TempData["message"] = $"The transaction has been successfully renewed for {renewalPeriod} days.";
                TempData["messageType"] = "success";
                return RedirectToAction("TransactionsIndex");
            }
            catch (Exception ex)
            {
                TempData["message"] = "An error occurred: " + ex.Message;
                TempData["messageType"] = "error";
                return RedirectToAction("TransactionsIndex");
            }
        }



        // Mark book as lost
        [HttpPost]
        public async Task<IActionResult> MarkAsLost(int id, [FromServices] EbayAuthService authService)
        {
            try
            {
                var transaction = await _context.Transactions
                    .Include(t => t.User)       //borrower
                    .Include(t => t.BookCopy)
                        .ThenInclude(bc => bc.Book)
                    .FirstOrDefaultAsync(t => t.Id == id);

                if (transaction == null) return NotFound();

                transaction.Status = "Lost";
                transaction.BookCopy.Status = "Lost";

                decimal? lostPenaltyAmount = null;
                string? isbn = transaction.BookCopy?.Book?.ISBN;

                if (!string.IsNullOrEmpty(isbn))
                {
                    var token = await authService.GetAccessTokenAsync();
                    if (!string.IsNullOrEmpty(token))
                    {
                        var marketPrice = await _ebayBookService.GetBookPriceInPesoAsync(isbn, token);
                        if (marketPrice.HasValue)
                            lostPenaltyAmount = marketPrice.Value;
                    }
                }

                if (!lostPenaltyAmount.HasValue)
                {
                    var manualModel = new ManualPenaltyViewModel
                    {
                        TransactionId = transaction.Id,
                        Reason = "Lost",
                        ISBN = isbn,
                        SuggestedAmount = null,
                        BookTitle = transaction.BookCopy?.Book?.Title ?? "Unknown Title",
                    };
                    return View("ManualPenalty", manualModel);
                }

                var existingPenalty = await _context.Penalties
                    .FirstOrDefaultAsync(p => p.TransactionID == transaction.Id && p.Reason == "Lost");

                if (existingPenalty == null)
                {
                    _context.Penalties.Add(new Penalty
                    {
                        TransactionID = transaction.Id,
                        Reason = "Lost",
                        Amount = lostPenaltyAmount.Value,
                        IsPaid = false,
                        CreatedAt = DateTime.Now
                    });
                }
                else
                {
                    existingPenalty.Amount = lostPenaltyAmount.Value;
                    existingPenalty.IsPaid = false;
                }

                _context.Update(transaction);
                await _context.SaveChangesAsync();

                // Enrich borrower info
                var student = _context.Students.FirstOrDefault(s => s.Email == transaction.User.Email);
                var staff = _context.Staffs.FirstOrDefault(st => st.Email == transaction.User.Email);

                string borrowerName = student != null
                    ? $"{student.FirstName} {student.LastName}"
                    : staff != null
                        ? $"{staff.FirstName} {staff.LastName}"
                        : transaction.User.Name;

                _logService.LogAction(
                    UserId,
                    "Book Marked as Lost",
                    $"{Username} marked '{transaction?.BookCopy?.Book?.Title}' (Copy #{transaction?.BookCopy.CopyNumber}) as lost for {borrowerName}. Penalty ₱{lostPenaltyAmount.Value:F2} applied."
                );

                TempData["message"] = $"Book marked as lost. Penalty fee ₱{lostPenaltyAmount.Value:F2} applied.";
                TempData["messageType"] = "warning";
                return RedirectToAction("TransactionsIndex");
            }
            catch (Exception ex)
            {
                TempData["message"] = "An error occurred: " + ex.Message;
                TempData["messageType"] = "error";
                return RedirectToAction("TransactionsIndex");
            }
        }


        [HttpPost]
        public async Task<IActionResult> ManualPenalty(ManualPenaltyViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var transaction = await _context.Transactions
                .Include(t => t.BookCopy)
                .FirstOrDefaultAsync(t => t.Id == model.TransactionId);

            if (transaction == null)
                return NotFound();

            transaction.Status = "Lost";
            transaction.BookCopy.Status = "Lost";

            var existingPenalty = await _context.Penalties
                .FirstOrDefaultAsync(p => p.TransactionID == transaction.Id && p.Reason == "Lost");

            if (existingPenalty == null)
            {
                _context.Penalties.Add(new Penalty
                {
                    TransactionID = transaction.Id,
                    Reason = model.Reason,
                    Amount = model.Amount,
                    IsPaid = false,
                    CreatedAt = DateTime.Now
                });
            }
            else
            {
                existingPenalty.Amount = model.Amount;
                existingPenalty.IsPaid = false;
            }

            _context.Update(transaction);
            await _context.SaveChangesAsync();

            _logService.LogAction(
                UserId,
                "Manual Lost Penalty",
                $"{Username} manually set lost penalty for '{transaction?.BookCopy?.Book?.Title}' (Copy #{transaction?.BookCopy.CopyNumber}) to ₱{model.Amount:F2}."
            );

            TempData["message"] = $"Penalty of ₱{model.Amount:F2} set and book marked as lost.";
            TempData["messageType"] = "warning";

            return RedirectToAction("TransactionsIndex");
        }


        [HttpPost]
        public async Task<IActionResult> MarkAsDamaged(int id)
        {
            try
            {
                var transaction = await _context.Transactions
                    .Include(t => t.User)       //borrower
                    .Include(t => t.BookCopy)
                    .FirstOrDefaultAsync(t => t.Id == id);

                if (transaction == null) return NotFound();

                transaction.Status = "Damaged";
                transaction.BookCopy.Status = "Damaged";

                decimal damagedPenalty = 300;

                var existingPenalty = await _context.Penalties
                    .FirstOrDefaultAsync(p => p.TransactionID == transaction.Id && p.Reason == "Damaged");

                if (existingPenalty == null)
                {
                    _context.Penalties.Add(new Penalty
                    {
                        TransactionID = transaction.Id,
                        Reason = "Damaged",
                        Amount = damagedPenalty,
                        IsPaid = false,
                        CreatedAt = DateTime.Now
                    });
                }
                else
                {
                    existingPenalty.Amount = damagedPenalty;
                    existingPenalty.IsPaid = false;
                }

                _context.Update(transaction);
                await _context.SaveChangesAsync();

                //Enrich borrower info
                var student = _context.Students.FirstOrDefault(s => s.Email == transaction.User.Email);
                var staff = _context.Staffs.FirstOrDefault(st => st.Email == transaction.User.Email);

                string borrowerName = student != null
                    ? $"{student.FirstName} {student.LastName}"
                    : staff != null
                        ? $"{staff.FirstName} {staff.LastName}"
                        : transaction.User.Name;

                _logService.LogAction(
                    UserId,
                    "Book Marked as Damaged",
                    $"{Username} marked '{transaction?.BookCopy?.Book?.Title}' (Copy #{transaction?.BookCopy.CopyNumber}) as damaged for {borrowerName}. A penalty of ₱{damagedPenalty:F2} has been applied."
                );

                TempData["message"] = "The book has been marked as damaged. A penalty fee has been applied.";
                TempData["messageType"] = "warning";
                return RedirectToAction("TransactionsIndex");
            }
            catch (Exception ex)
            {
                TempData["message"] = "An error occurred: " + ex.Message;
                TempData["messageType"] = "error";
                return RedirectToAction("TransactionsIndex");
            }
        }


        [HttpGet]
        public IActionResult OverdueIndex(string searchQuery, string dueDateFilter, int? overdueDaysFilter, int? page)
        {
            int pageSize = 10;
            int pageNumber = page ?? 1;

            var overdueQuery = _context.Overdues
                .Include(o => o.Transaction)
                    .ThenInclude(t => t.User) // borrower always a User
                .Include(o => o.Transaction)
                    .ThenInclude(t => t.BookCopy)
                        .ThenInclude(bc => bc.Book)
                .Where(o => o.Transaction.DueDate < DateTime.Now)
                .AsQueryable();

            // Search by borrower or book
            if (!string.IsNullOrEmpty(searchQuery))
            {
                overdueQuery = overdueQuery.Where(o =>
                    o.Transaction.User.Name.Contains(searchQuery) ||
                    o.Transaction.BookCopy.Book.Title.Contains(searchQuery) ||
                    o.Transaction.BookCopy.CopyNumber.Contains(searchQuery));
            }

            // Filter by due date
            if (!string.IsNullOrEmpty(dueDateFilter) && DateTime.TryParse(dueDateFilter, out DateTime parsedDueDate))
            {
                overdueQuery = overdueQuery.Where(o => o.Transaction.DueDate.Date == parsedDueDate.Date);
            }

            // Filter by overdue days
            if (overdueDaysFilter.HasValue)
            {
                overdueQuery = overdueQuery.Where(o => o.OverdueDays >= overdueDaysFilter.Value);
            }

            var overdueList = overdueQuery
                .Select(o => new OverdueViewModel
                {
                    Id = o.Id,
                    TransactionID = o.TransactionID,
                    BookTitle = o.Transaction.BookCopy.Book.Title,
                    CopyNumber = o.Transaction.BookCopy.CopyNumber,
                    DueDate = o.Transaction.DueDate,
                    OverdueDays = o.OverdueDays,
                    FineAmount = o.Transaction.Penalty
                        .Where(p => p.Reason == "Overdue")
                        .Select(p => p.Amount)
                        .FirstOrDefault(),
                    TransactionStatus = o.Transaction.Status,

                    // Borrower Name (enriched)
                    BorrowerName =
                        (from s in _context.Students
                         where s.Email == o.Transaction.User.Email
                         select s.FirstName + " " + s.LastName).FirstOrDefault()
                        ??
                        (from st in _context.Staffs
                         where st.Email == o.Transaction.User.Email
                         select st.FirstName + " " + st.LastName).FirstOrDefault()
                        ?? o.Transaction.User.Name
                })
                .ToPagedList(pageNumber, pageSize);

            return View(overdueList);
        }


        [HttpGet]
        public async Task<IActionResult> SendReminder(int id)
        {
            try
            {
                var transaction = await _context.Transactions
                    .Include(t => t.User) //borrower always a User
                    .Include(t => t.BookCopy)
                        .ThenInclude(bc => bc.Book)
                    .FirstOrDefaultAsync(t => t.Id == id);

                if (transaction == null)
                {
                    TempData["message"] = "Transaction not found.";
                    TempData["messageType"] = "error";
                    return RedirectToAction("OverdueIndex");
                }

                var borrower = transaction.User;
                if (borrower == null)
                {
                    TempData["message"] = "User not found.";
                    TempData["messageType"] = "error";
                    return RedirectToAction("OverdueIndex");
                }

                if (string.IsNullOrEmpty(borrower.Email))
                {
                    TempData["message"] = "User email is missing.";
                    TempData["messageType"] = "error";
                    return RedirectToAction("OverdueIndex");
                }

                //Enrich borrower info
                var student = await _context.Students.FirstOrDefaultAsync(s => s.Email == borrower.Email);
                var staff = await _context.Staffs.FirstOrDefaultAsync(st => st.Email == borrower.Email);

                string borrowerName = student != null
                    ? $"{student.FirstName} {student.LastName}"
                    : staff != null
                        ? $"{staff.FirstName} {staff.LastName}"
                        : borrower.Name;

                string borrowerDetails = student != null
                    ? $"<p><strong>Program:</strong> {student.Program} | <strong>Year:</strong> {student.YearLevel}-{student.Section}</p>"
                    : staff != null
                        ? $"<p><strong>Department:</strong> {staff.Department} | <strong>Position:</strong> {staff.Position ?? "N/A"}</p>"
                        : "";

                // Collect overdue books
                var overdueBooks = await _context.Overdues
                    .Where(o => o.TransactionID == id)
                    .Include(o => o.Transaction)
                        .ThenInclude(t => t.BookCopy)
                        .ThenInclude(bc => bc.Book)
                    .ToListAsync();

                if (!overdueBooks.Any())
                {
                    TempData["message"] = "No overdue books found for this borrower.";
                    TempData["messageType"] = "info";
                    return RedirectToAction("OverdueIndex");
                }

                // Get penalties (if any)
                var penalty = await _context.Penalties
                    .Where(p => p.TransactionID == id && p.Reason == "Overdue")
                    .FirstOrDefaultAsync();

                string penaltyMessage = penalty != null
                    ? $"<p><strong>Penalty:</strong> ₱{penalty.Amount:F2} - {penalty.Reason}</p>"
                    : "<p><strong>Penalty:</strong> No penalties recorded yet.</p>";

                // Build email
                string subject = "Overdue Book Reminder";
                string bookList = string.Join("<br>", overdueBooks.Select(o =>
                    $"{o.Transaction.BookCopy.Book.Title} - Due on {o.Transaction.DueDate:MMMM dd, yyyy}"));

                string message = $@"
            <p>Dear {borrowerName},</p>
            <p>This is a friendly reminder that you have overdue books. Please return them as soon as possible to avoid additional fines.</p>
            <p><strong>Overdue Books:</strong></p>
            <p>{bookList}</p>
            {penaltyMessage}
            {borrowerDetails}
            <p>Best regards,</p>  
            <p><strong>The ShelfMaster Support</strong></p>";

                // Send
                await _emailService.SendEmailAsync(borrower.Email, subject, message);

                _logService.LogAction(
                    UserId,
                    "Overdue Email Reminder",
                    $"{Username} sent an overdue email reminder to {borrowerName}."
                );

                TempData["message"] = "Reminder email sent successfully!";
                TempData["messageType"] = "success";
            }
            catch (Exception ex)
            {
                TempData["message"] = "An error occurred: " + ex.Message;
                TempData["messageType"] = "error";
            }

            return RedirectToAction("OverdueIndex");
        }



        [HttpPost]
        public IActionResult ReturnAndPayFine(int id, int TransactionID)
        {
            try
            {
                var overdueRecord = _context.Overdues.FirstOrDefault(o => o.Id == id);
                var transaction = _context.Transactions
                    .Include(t => t.User)       //Users table
                    .Include(t => t.BookCopy)
                    .Include(t => t.Penalty)
                    .FirstOrDefault(t => t.Id == TransactionID);

                if (transaction == null)
                {
                    return NotFound();
                }

                transaction.ReturnDate = DateTime.Now;
                transaction.Status = "Completed";

                if (transaction.BookCopy != null)
                {
                    transaction.BookCopy.Status = "Available";
                }

                // Mark overdue penalty as paid
                var penalty = transaction.Penalty.FirstOrDefault(p => p.Reason == "Overdue");
                if (penalty != null)
                {
                    penalty.IsPaid = true;
                }

                _context.SaveChanges();

                // Enrich borrower info
                var student = _context.Students.FirstOrDefault(s => s.Email == transaction.User.Email);
                var staff = _context.Staffs.FirstOrDefault(st => st.Email == transaction.User.Email);

                string borrowerName = student != null
                    ? $"{student.FirstName} {student.LastName}"
                    : staff != null
                        ? $"{staff.FirstName} {staff.LastName}"
                        : transaction.User.Name;

                _logService.LogAction(
                    UserId,
                    "Book Return",
                    $"{Username} marked '{transaction?.BookCopy?.Book?.Title}' (Copy #{transaction?.BookCopy.CopyNumber}) as returned and fine paid by {borrowerName}."
                );

                TempData["message"] = "Transaction complete: the book has been returned and the fine has been marked as paid.";
                TempData["messageType"] = "success";
                return RedirectToAction("OverdueIndex");
            }
            catch (Exception ex)
            {
                TempData["message"] = "An error occurred: " + ex.Message;
                TempData["messageType"] = "error";
                return RedirectToAction("TransactionsIndex");
            }
        }


        [HttpGet]
        public async Task<IActionResult> PayFees(int transactionId)
        {
            var transaction = await _context.Transactions
            .Include(t => t.User) //make sure User is loaded
            .Include(t => t.Penalty)
            .Include(t => t.BookCopy)
                .ThenInclude(bc => bc.Book)
            .FirstOrDefaultAsync(t => t.Id == transactionId);

            if (transaction == null) return NotFound();

            var borrowerName =
                await _context.Students
                    .Where(s => s.Email == transaction.User.Email) //match by Email
                    .Select(s => s.FirstName + " " + s.LastName)
                    .FirstOrDefaultAsync()
                ??
                await _context.Staffs
                    .Where(st => st.Email == transaction.User.Email) //match by Email
                    .Select(st => st.FirstName + " " + st.LastName)
                    .FirstOrDefaultAsync()
                ?? transaction.User.Name;


            var model = new PaymentViewModel
            {
                TransactionID = transaction.Id,
                BorrowerID = transaction.BorrowerID,
                BorrowerName = borrowerName ?? transaction.User.Name ?? "Unknown Borrower",
                Penalties = transaction.Penalty?.ToList() ?? new List<Penalty>(),
                Amount = transaction.Penalty?.Sum(p => p.Amount) ?? 0m,
                Reason = transaction.Penalty != null ? string.Join(", ", transaction.Penalty.Select(p => p.Reason)) : "",
                Method = "Cashier",  //Default, read-only
                PaidOn = "Not Paid"
            };

            return View(model);
        }




        [HttpPost]
        public IActionResult ProcessPayment(int transactionId, string orNumber)
        {
            try
            {
                var transaction = _context.Transactions
                    .Include(t => t.User)        //Users table
                    .Include(t => t.Penalty)
                    .Include(t => t.BookCopy)
                    .FirstOrDefault(t => t.Id == transactionId);

                if (transaction == null) return NotFound();

                decimal totalAmount = transaction.Penalty?.Sum(p => p.Amount) ?? 0;

                // Mark penalties as paid
                foreach (var penalty in transaction.Penalty)
                {
                    penalty.IsPaid = true;
                }

                // Save payment with OR number
                var payment = new Payment
                {
                    TransactionID = transactionId,
                    BorrowerID = transaction.BorrowerID,
                    Amount = totalAmount,
                    Method = "Cashier",
                    ORNumber = orNumber,
                    PaymentDate = DateTime.Now
                };

                _context.Payments.Add(payment);

                // Close transaction if all penalties cleared
                bool allPenaltiesPaid = transaction.Penalty.All(p => p.IsPaid);
                if (allPenaltiesPaid)
                {
                    transaction.Status = "Completed";
                    transaction.ReturnDate = DateTime.Now;

                    if (transaction.BookCopy != null &&
                        !transaction.Penalty.Any(p => p.Reason == "Lost" || p.Reason == "Damaged"))
                    {
                        transaction.BookCopy.Status = "Available";
                        _context.BookCopies.Update(transaction.BookCopy);
                    }
                }

                _context.SaveChanges();

                //Enrich borrower info
                var student = _context.Students.FirstOrDefault(s => s.Email == transaction.User.Email);
                var staff = _context.Staffs.FirstOrDefault(st => st.Email == transaction.User.Email);

                string borrowerName = student != null
                    ? $"{student.FirstName} {student.LastName}"
                    : staff != null
                        ? $"{staff.FirstName} {staff.LastName}"
                        : transaction.User.Name;

                _logService.LogAction(
                    UserId,
                    "Payment Recorded",
                    $"{Username} recorded payment for Transaction #{transaction.Id} ({borrowerName}) with OR No. {orNumber} (₱{totalAmount:N2})."
                );

                TempData["message"] = $"Payment recorded successfully (OR No. {orNumber}).";
                TempData["messageType"] = "success";
                return RedirectToAction("TransactionsIndex");
            }
            catch (Exception ex)
            {
                TempData["message"] = "An error occurred: " + ex.Message;
                TempData["messageType"] = "error";
                return RedirectToAction("TransactionsIndex");
            }
        }



        [HttpGet]
        public async Task<IActionResult> Payments(int? page)
        {
            int pageSize = 10;
            int pageNumber = page ?? 1;

            var combinedPayments = await _context.Penalties
                .Include(p => p.Transaction)
                    .ThenInclude(t => t.User) 
                .GroupJoin(
                    _context.Payments,
                    penalty => penalty.TransactionID,
                    payment => payment.TransactionID,
                    (penalty, payments) => new { penalty, payments }
                )
                .SelectMany(
                    x => x.payments.DefaultIfEmpty(),
                    (x, payment) => new PaymentViewModel
                    {
                        TransactionID = x.penalty.TransactionID,
                        Reason = x.penalty.Reason,
                        Amount = x.penalty.Amount,
                        Method = payment != null ? payment.Method : "Unpaid",
                        PaidOn = payment != null ? payment.PaymentDate.ToString("MMMM dd, yyyy") : "Not Paid",
                        BorrowerID = x.penalty.Transaction.BorrowerID,

                        // Borrower Name (Users + Student/Staff enrichment)
                        BorrowerName =
                            (from s in _context.Students
                             where s.Email == x.penalty.Transaction.User.Email
                             select s.FirstName + " " + s.LastName).FirstOrDefault()
                            ??
                            (from st in _context.Staffs
                             where st.Email == x.penalty.Transaction.User.Email
                             select st.FirstName + " " + st.LastName).FirstOrDefault()
                            ?? x.penalty.Transaction.User.Name
                    })
                .ToListAsync();

            var pagedPayments = combinedPayments.ToPagedList(pageNumber, pageSize);
            return View(pagedPayments);
        }


        [HttpGet]
        public IActionResult GetBookByCode(string code)
        {
            if (string.IsNullOrEmpty(code)) return BadRequest();

            // Handle Book QR (BOOK-123)
            if (code.StartsWith("BOOK-"))
            {
                if (int.TryParse(code.Replace("BOOK-", ""), out int id))
                {
                    var book = _context.Books.FirstOrDefault(b => b.Id == id);
                    if (book != null)
                    {
                        return Json(new { id = book.Id, title = book.Title });
                    }
                }
            }

            // Handle Copy QR (COPY-C110, COPY-ACQ000123-C001)
            if (code.StartsWith("COPY-"))
            {
                var copyNumber = code.Replace("COPY-", "");
                var bookCopy = _context.BookCopies
                    .Include(bc => bc.Book)
                    .FirstOrDefault(bc => bc.CopyNumber == copyNumber);

                if (bookCopy != null)
                {
                    return Json(new { id = bookCopy.Id, title = bookCopy.Book.Title });
                }
            }

            return NotFound();
        }


        // Add these methods to your LendingController

        [HttpGet]
        public IActionResult SearchBorrowers(string term)
        {
            if (string.IsNullOrWhiteSpace(term) || term.Length < 2)
            {
                return Json(new List<object>());
            }

            var borrowers = new List<object>();

            // --- Students ---
            var students = (from s in _context.Students
                            join u in _context.Users on s.Email equals u.Email
                            where s.Status == StudentStatus.Enrolled
                                  && u.Status == UserStatus.Active
                                  && u.IsArchived == false
                            select new
                            {
                                Id = u.Id,
                                Name = $"{s.FirstName} {s.LastName}",
                                Type = "Student",
                                Email = s.Email,
                                StudentNumber = s.StudentNumber,
                                Department = s.Department,
                                Program = s.Program,
                                YearLevel = s.YearLevel,
                                Section = s.Section,
                                DisplayText = $"{s.StudentNumber} - {s.FirstName} {s.LastName} ({s.Program} {s.YearLevel}-{s.Section})"
                            }).Take(10).ToList();

            // --- Staff ---
            var staff = (from st in _context.Staffs
                         join u in _context.Users on st.Email equals u.Email
                         where u.Status == UserStatus.Active
                               && u.IsArchived == false
                               && (st.StaffNumber.Contains(term) ||
                                   st.FirstName.Contains(term) ||
                                   st.LastName.Contains(term))
                         select new
                         {
                             Id = u.Id,
                             Name = $"{st.FirstName} {st.LastName}",
                             Type = "Staff",
                             Email = st.Email,
                             StaffNumber = st.StaffNumber,    
                             Department = st.Department,      
                             Position = st.Position ?? "N/A",
                             DisplayText = $"{st.StaffNumber} - {st.FirstName} {st.LastName} ({st.Department} - {st.Position ?? "N/A"})"
                         })
                         .Take(10)
                         .ToList();


            borrowers.AddRange(students.Cast<object>());
            borrowers.AddRange(staff.Cast<object>());

            return Json(borrowers.Take(15));
        }


    }
}
