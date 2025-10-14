using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using X.PagedList.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using test.Areas.Admin.Controllers;
using test.Data;
using test.Services;
using test.Areas.Admin.Models;
using test.Entity;

namespace test.Areas.Admin.Controllers
{
    [Authorize]
    [Area("Admin")]
    public class BorrowerController : BaseController
    {
        private readonly ApplicationDbContext _context;
        private readonly PasswordHasher<string> _passwordHasher = new PasswordHasher<string>();
        private readonly EmailService _emailService;
        private readonly LogService _logService;


        public BorrowerController(ApplicationDbContext context, EmailService emailService, LogService logService)
        {
            _context = context;
            _emailService = emailService;
            _logService = logService;
        }

        [HttpGet]
        public IActionResult Index(string searchQuery, int? page)
        {
            int pageSize = 10;
            int pageNumber = page ?? 1;

            var borrowers = _context.Users
            .Where(u => u.Role == "Borrower" && !u.IsArchived) 
            .Select(u => new BorrowerViewModel
            {
                Id = u.Id,
                Name = u.Name,
                Email = u.Email,
                ContactNum = u.ContactNum,
                BirthDate = u.BirthDate,
                Gender = u.Gender,
                IsVerified = u.IsVerified,
                CreatedAt = u.CreatedAt,
                UpdatedAt = u.UpdatedAt,
                Transactions = u.Transaction
            });


            if (!string.IsNullOrEmpty(searchQuery))
            {
                borrowers = borrowers.Where(b =>
                    b.Name.Contains(searchQuery) ||
                    b.Email.Contains(searchQuery) ||
                    b.ContactNum.Contains(searchQuery));
            }
            var pagedList = borrowers.ToPagedList(pageNumber, pageSize);
            return View(pagedList);
        }

        public IActionResult New()
        {
            return View();
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateAccount(BorrowerViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View("New", model);
            }

            try
            {
                bool emailExists = await _context.Users.AnyAsync(u => u.Email == model.Email);
                if (emailExists)
                {
                    ModelState.AddModelError("Email", "This email is already registered.");
                    return View("New", model);
                }

                // Check if email exists in Students or Staffs
                bool isStudent = await _context.Students.AnyAsync(s => s.Email == model.Email);
                bool isStaff = await _context.Staffs.AnyAsync(st => st.Email == model.Email);

                if (!isStudent && !isStaff)
                {
                    ModelState.AddModelError("Email", "This email is not registered in Students or Staffs.");
                    return View("New", model);
                }

                // Generate username + password
                string sanitizedUsername = model.Name.ToLower().Replace(" ", "");
                string rawPassword = sanitizedUsername + "123";
                string hashedPassword = _passwordHasher.HashPassword(null, rawPassword);

                var newUser = new User
                {
                    Name = model.Name,
                    ContactNum = model.ContactNum,
                    BirthDate = model.BirthDate,
                    Gender = model.Gender,
                    Role = "Borrower",
                    Username = sanitizedUsername,
                    Email = model.Email,
                    IsVerified = false,
                    VerificationToken = Guid.NewGuid().ToString(),
                    Password = hashedPassword,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now,
                    IsArchived = false
                };

                _context.Users.Add(newUser);
                await _context.SaveChangesAsync();
                _logService.LogAction(UserId, "New Borrower", $"{Username} created a new borrower with ID #{newUser.Id}.");

                var verificationLink = $"{Request.Scheme}://{Request.Host}/api/auth/verify?token={newUser.VerificationToken}";
                await _emailService.SendEmailAsync(newUser.Email, "Verify Your Email",
                    $@"<p>Dear {newUser.Name},</p>  
            <p>Please verify your email: <a href='{verificationLink}'>Verify</a></p>");

                TempData["message"] = "Borrower account created successfully!";
                TempData["messageType"] = "success";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                var innerException = ex.InnerException?.Message ?? ex.Message;
                ModelState.AddModelError(string.Empty, "An error occurred: " + innerException);
                return View("New", model);
            }
        }


        public IActionResult Details(int id, string sortStatus)
        {
            var borrower = _context.Users
                .Where(u => u.Id == id && u.Role == "Borrower")
                .Include(u => u.Transaction)
                    .ThenInclude(t => t.BookCopy)
                        .ThenInclude(bc => bc.Book)
                .Select(u => new BorrowerViewModel
                {
                    Id = u.Id,
                    Name = u.Name,
                    Email = u.Email,
                    ContactNum = u.ContactNum,
                    BirthDate = u.BirthDate,
                    Gender = u.Gender,
                    Transactions = u.Transaction.Select(t => new Transaction
                    {
                        Id = t.Id,
                        BookCopyID = t.BookCopy != null ? t.BookCopy.Id : 0,
                        TransactionDate = t.TransactionDate,
                        DueDate = t.DueDate,
                        ReturnDate = t.ReturnDate,
                        Status = t.Status,
                        BookCopy = new BookCopy
                        {
                            CopyNumber = t.BookCopy.CopyNumber,
                            Status = t.BookCopy.Status,
                            Book = new Book
                            {
                                Title = t.BookCopy.Book.Title,
                            }
                        }
                    }).ToList()
                })
                .FirstOrDefault();

            if (borrower == null)
            {
                TempData["message"] = "Borrower not found.";
                TempData["messageType"] = "error";
                return RedirectToAction("Index");
            }

            // Apply filter
            if (!string.IsNullOrEmpty(sortStatus))
            {
                borrower.Transactions = borrower.Transactions
                    .Where(t => t.BookCopy?.Status == sortStatus)
                    .ToList();
            }

            return View(borrower);
        }



        public async Task<IActionResult> Update(int id, string Name, DateOnly BirthDate, string ContactNum, string Email)
        {
            try
            {
                var borrower = await _context.Users.FindAsync(id);
                if (borrower == null)
                {
                    return NotFound();
                }

                borrower.Name = Name;
                borrower.BirthDate = BirthDate;
                borrower.ContactNum = ContactNum;
                borrower.Email = Email;

                borrower.UpdatedAt = DateTime.Now;
                _context.SaveChanges();
                _logService.LogAction(UserId, "Update Borrpwer's Information", $"{Username} updated the borrower's information with ID #{borrower.Id}.");

                TempData["Message"] = "Borrower's information has been updated successfully!";
                TempData["messageType"] = "success";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["message"] = "An error occurred: " + ex.Message;
                TempData["messageType"] = "error";
                return RedirectToAction("Index");
            }
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var borrower = await _context.Users.FindAsync(id);
                if (borrower == null)
                {
                    return NotFound();
                }

                borrower.IsArchived = true;
                borrower.UpdatedAt = DateTime.Now;

                _context.Users.Update(borrower);
                await _context.SaveChangesAsync();

                _logService.LogAction(UserId, "Archive Borrower", $"{Username} archived the borrower with ID #{borrower.Id}.");

                TempData["message"] = "Borrower's account has been archived successfully!";
                TempData["messageType"] = "success";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["message"] = "An error occurred: " + ex.Message;
                TempData["messageType"] = "error";
                return RedirectToAction("Index");
            }
        }

        [HttpGet]
        public IActionResult Archived(string searchQuery, int? page)
        {
            int pageSize = 10;
            int pageNumber = page ?? 1;

            var borrowers = _context.Users
                .Where(u => u.Role == "Borrower" && u.IsArchived) // only archived
                .Select(u => new BorrowerViewModel
                {
                    Id = u.Id,
                    Name = u.Name,
                    Email = u.Email,
                    ContactNum = u.ContactNum,
                    BirthDate = u.BirthDate,
                    Gender = u.Gender,
                    IsVerified = u.IsVerified,
                    CreatedAt = u.CreatedAt,
                    UpdatedAt = u.UpdatedAt,
                    Transactions = u.Transaction
                });

            if (!string.IsNullOrEmpty(searchQuery))
            {
                borrowers = borrowers.Where(b =>
                    b.Name.Contains(searchQuery) ||
                    b.Email.Contains(searchQuery) ||
                    b.ContactNum.Contains(searchQuery));
            }

            var pagedList = borrowers.ToPagedList(pageNumber, pageSize);
            return View(pagedList);
        }

        [HttpPost]
        public async Task<IActionResult> Restore(int id)
        {
            try
            {
                var borrower = await _context.Users.FindAsync(id);
                if (borrower == null)
                {
                    return NotFound();
                }

                borrower.IsArchived = false;
                borrower.UpdatedAt = DateTime.Now;

                _context.Users.Update(borrower);
                await _context.SaveChangesAsync();

                _logService.LogAction(UserId, "Restore Borrower", $"{Username} restored borrower ID #{borrower.Id}.");

                TempData["message"] = "Borrower restored successfully!";
                TempData["messageType"] = "success";
                return RedirectToAction("Archived");
            }
            catch (Exception ex)
            {
                TempData["message"] = "An error occurred: " + ex.Message;
                TempData["messageType"] = "error";
                return RedirectToAction("Archived");
            }
        }
    }
}
