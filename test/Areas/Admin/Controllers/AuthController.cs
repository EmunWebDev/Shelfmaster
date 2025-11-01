using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using System.Security.Claims;
using test.Data;
using test.Services;

namespace test.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class AuthController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly PasswordHasher<string> _passwordHasher = new PasswordHasher<string>();
        private readonly LogService _logService;

        public AuthController(ApplicationDbContext context, LogService logService)
        {
            _context = context;
            _logService = logService;
        }

        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public IActionResult Login(string Username, string Password)
        {
            if (string.IsNullOrWhiteSpace(Password) || Password.Length < 12)
            {
                ViewBag.Error = "Password must be at least 12 characters long.";
                return View();
            }

            var user = _context.Users.FirstOrDefault(u => u.Username == Username && (u.Role == "Admin" || u.Role == "Librarian"));

            if (user == null)
            {
                ViewBag.Error = "Invalid username or password.";
                return View();
            }

            var result = _passwordHasher.VerifyHashedPassword(null, user.Password, Password);

            if (result == PasswordVerificationResult.Success)
            {
                var claims = new List<Claim>
                    {
                        new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                        new Claim(ClaimTypes.Name, user.Name),
                        new Claim(ClaimTypes.Email, user.Email),
                        new Claim(ClaimTypes.Role, user.Role)
                    };

                var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                var authProperties = new AuthenticationProperties
                {
                    IsPersistent = true 
                };

                HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity));
                _logService.LogAction(user.Id, "Logged In", $"User {user.Username} logged in.");

                return RedirectToAction("Index", "Dashboard");
            }

            ViewBag.Error = "Invalid username or password.";
            return View();

        }
        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            var usernameClaim = User.FindFirst(ClaimTypes.Name);

            if (userIdClaim != null && usernameClaim != null)
            {
                int userId = int.Parse(userIdClaim.Value);
                string username = usernameClaim.Value;

                _logService.LogAction(userId, "Logged Out", $"User {username} logged out.");
            }

            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login");
        }


    }
}
