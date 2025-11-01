
using test.Data;
using test.Entity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using X.PagedList.Extensions;
using test.Services;
using Microsoft.AspNetCore.Mvc;
using test.Areas.Admin.Controllers;

namespace test.Areas.Admin.Controllers
{
    [Authorize(Roles = "Admin")]
    [Area("Admin")]
    public class UserController : BaseController
    {
        private readonly ApplicationDbContext _context;
        private readonly PasswordHasher<string> _passwordHasher = new PasswordHasher<string>();
        private readonly LogService _logService;

        public UserController(ApplicationDbContext context, LogService logService)
        {
            _context = context;
            _logService = logService;
        }

        [HttpGet]
        public IActionResult Index(string searchQuery, string roleFilter, int? page)
        {

            int pageSize = 10;
            int pageNumber = page ?? 1;

            var users = _context.Users
                .Where(u => u.Role == "Librarian" || u.Role == "Admin") 
                .AsQueryable();

            if (!string.IsNullOrEmpty(searchQuery))
            {
                users = users.Where(u =>
                    u.Name.Contains(searchQuery) ||
                    u.Username.Contains(searchQuery));
            }

            if (!string.IsNullOrEmpty(roleFilter))
            {
                users = users.Where(u => u.Role == roleFilter);
            }
   
            var pagedList = users.ToPagedList(pageNumber, pageSize);
            return View(pagedList);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Register(string Name, string Email, string Role)
        {
            try
            {
                //Check if staff exists in Staff table
                var staff = _context.Staffs.FirstOrDefault(s => s.Email == Email && s.IsActive);
                if (staff == null)
                {
                    TempData["message"] = "This email is not associated with an active staff member!";
                    TempData["messageType"] = "error";
                    return RedirectToAction("Index");
                }

                //Prevent duplicate registration in Users
                bool emailExists = _context.Users.Any(u => u.Email == Email);
                if (emailExists)
                {
                    TempData["message"] = "This email is already registered!";
                    TempData["messageType"] = "error";
                    return RedirectToAction("Index");
                }

                // Generate Username
                string sanitizedUsername = Name.ToLower().Replace(" ", "");
                string generatedUsername = (Role == "Admin")
                    ? $"admin{sanitizedUsername}123"
                    : $"{sanitizedUsername}123";

                string rawPassword = sanitizedUsername + "123123123";
                string hashedPassword = _passwordHasher.HashPassword(null, rawPassword);

                var user = new User
                {
                    Name = Name,
                    Email = Email,
                    IsVerified = true,
                    Role = Role, // "Admin" or "Librarian"
                    Username = generatedUsername,
                    Password = hashedPassword,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };

                _context.Users.Add(user);
                _context.SaveChanges();

                _logService.LogAction(
                    UserId,
                    "Account Creation",
                    $"{Username} created an account for {user.Name} with role of {user.Role}."
                );

                TempData["message"] = "Account created successfully!";
                TempData["messageType"] = "success";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                var innerException = ex.InnerException?.Message ?? ex.Message;
                TempData["message"] = "An error occurred: " + innerException;
                TempData["messageType"] = "error";
                return RedirectToAction("Index");
            }
        }

    }
}
