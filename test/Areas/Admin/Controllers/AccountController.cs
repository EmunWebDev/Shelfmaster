using test.Areas.Admin.Models;
using test.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace test.Areas.Admin.Controllers
{
    [Authorize]
    [Area("Admin")]
    public class AccountController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly PasswordHasher<string> _passwordHasher = new PasswordHasher<string>();

        public AccountController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public IActionResult Profile(int id)
        {
            int userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));

            if (id != userId)
            {
                return Unauthorized();
            }

            var user = _context.Users
                .Where(u => u.Id == id)
                .Select(u => new BorrowerViewModel
                {
                    Id = u.Id,
                    Name = u.Name,
                    ContactNum = u.ContactNum,
                    BirthDate = u.BirthDate,
                    Gender = u.Gender,
                    Role = u.Role,
                    Username = u.Username,
                    Email = u.Email,
                    IsVerified = u.IsVerified,
                    CreatedAt = u.CreatedAt
                })
                .FirstOrDefault();

            if (user == null)
            {
                return NotFound();
            }

            return View(user);
        }

        [HttpPost]
        public IActionResult Update([FromBody] BorrowerViewModel model)
        {
            if (model == null)
            {
                return Json(new { success = false, message = "Invalid data." });
            }

            var borrower = _context.Users.FirstOrDefault(b => b.Id == model.Id);
            if (borrower == null)
            {
                return Json(new { success = false, message = "Borrower not found." });
            }

            borrower.Name = model.Name ?? borrower.Name;
            borrower.BirthDate = model.BirthDate ?? borrower.BirthDate;
            borrower.ContactNum = model.ContactNum ?? borrower.ContactNum;
            borrower.Gender = model.Gender ?? borrower.Gender;
            borrower.Username = model.Username ?? borrower.Username;
            borrower.Email = model.Email ?? borrower.Email;

            try
            {
                _context.SaveChanges();
                return Json(new { success = true, message = "Profile updated successfully!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error updating profile: " + ex.Message });
            }
        }

        [HttpGet]
        public IActionResult ChangeEmail(int id)
        {
            var user = _context.Users
                .Where(u => u.Id == id)
                .Select(u => new BorrowerViewModel
                {
                    Id = u.Id,
                    Email = u.Email,
                    IsVerified = u.IsVerified,
                })
                .FirstOrDefault();

            if (user == null)
            {
                return NotFound();
            }

            return View(user);
        }

        [HttpPost]
        public async Task<IActionResult> ChangeEmail(BorrowerViewModel model)
        {
            try
            {
                if (model == null || string.IsNullOrWhiteSpace(model.Email))
                {
                    ModelState.AddModelError("Email", "New email is required.");
                    return View(model);
                }

                var user = _context.Users.FirstOrDefault(u => u.Id == model.Id);
                if (user == null)
                {
                    return NotFound();
                }

                if (user.Email == model.Email)
                {
                    ModelState.AddModelError("Email", "New email must be different from the current email.");
                    return View(model);
                }

                bool emailExists = _context.Users.Any(u => u.Email == model.Email && u.Id != model.Id);
                if (emailExists)
                {
                    ModelState.AddModelError("Email", "This email is already registered with another account.");
                    return View(model);
                }

                user.Email = model.Email;
                user.IsVerified = true;
                _context.SaveChanges();


                TempData["message"] = "Your email has been updated.";
                TempData["messageType"] = "success";
                return RedirectToAction("Profile", new { id = model.Id });
            }
            catch (Exception ex)
            {
                TempData["message"] = "An error occurred while processing your request: " + ex.Message;
                TempData["messageType"] = "error";
                return RedirectToAction("Profile", new { id = model.Id });
            }
        }

        [HttpGet]
        public IActionResult ChangePassword(int id)
        {
            var user = _context.Users
                .Where(u => u.Id == id)
                .Select(u => new BorrowerViewModel
                {
                    Id = u.Id,
                    Email = u.Email,
                    Password = u.Password
                })
                .FirstOrDefault();

            if (user == null)
            {
                return NotFound();
            }

            return View(user);
        }

        [HttpPost]
        public IActionResult ChangePassword(BorrowerViewModel model, string confirmPassword)
        {
            try
            {
                if (model == null || string.IsNullOrWhiteSpace(model.Password))
                {
                    ModelState.AddModelError("Password", "New password is required.");
                    return View(model);
                }

                var user = _context.Users.FirstOrDefault(u => u.Id == model.Id);
                if (user == null)
                {
                    return NotFound();
                }

                if (model.Password != confirmPassword)
                {
                    ModelState.AddModelError("Password", "Passwords do not match.");
                    return View(model);
                }

                string hashedPassword = _passwordHasher.HashPassword(null, model.Password);
                user.Password = hashedPassword;
                _context.SaveChanges();

                TempData["message"] = "Your password has been updated successfully.";
                TempData["messageType"] = "success";

                return RedirectToAction("Profile", new { id = model.Id });
            }
            catch (Exception ex)
            {
                TempData["message"] = "An error occurred while processing your request: " + ex.Message;
                TempData["messageType"] = "error";
                return RedirectToAction("Profile", new { id = model.Id });
            }
        }
    }
}
