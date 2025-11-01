using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OtpNet;
using System.Security.Claims;
using test.Data;
using test.Entity;
using test.Models;
using test.Services;

namespace test.Controllers
{
    public class AuthController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly EmailService _emailService;
        private readonly PasswordHasher<string> _passwordHasher = new PasswordHasher<string>();
        private readonly OtpService _otpService;

        public AuthController(ApplicationDbContext context, EmailService emailService, OtpService otpService)
        {
            _context = context;
            _emailService = emailService;
            _otpService = otpService;
        }

        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(string Email, string Password, bool RememberMe)
        {
            if (string.IsNullOrEmpty(Email) || string.IsNullOrEmpty(Password)) 
                { ModelState.AddModelError("Email", "Email and password are required."); 
                return View(); 
            }

            if (Password.Length < 12)
            {
                ModelState.AddModelError("Password", "Password must be at least 12 characters long.");
                return View();
            }
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == Email);

            if (user == null)
            {
                // Notify user of failed login attempt
                _ = _emailService.SendEmailAsync(Email, "Failed Login Attempt",
                    "<p>Someone tried to log in to your ShelfMaster account, but the credentials were incorrect. " +
                    "If this wasn’t you, please reset your password immediately.</p>");

                ModelState.AddModelError("Email", "These credentials do not match our records.");
                return View();
            }

            if ((bool)!user.IsVerified)
            {
                ModelState.AddModelError("Email", "Please verify your email before logging in.");
                return View();
            }

            var passwordCheck = _passwordHasher.VerifyHashedPassword(null, user.Password, Password);
            if (passwordCheck != PasswordVerificationResult.Success)
            {
                _ = _emailService.SendEmailAsync(user.Email, "Failed Login Attempt",
                    "<p>Someone tried to log in to your ShelfMaster account but failed. If this wasn't you, change your password immediately.</p>");

                ModelState.AddModelError("Password", "Incorrect password.");
                return View();
            }

            // If user has 2FA enabled, redirect to 2FA page
            if (user.IsTwoFactorEnabled)
            {
                HttpContext.Session.SetInt32("2FAUserId", user.Id);
                return RedirectToAction(nameof(VerifyTwoFactorAuth));
            }

            // Otherwise, sign in user immediately
            await SignInUserAsync(user, RememberMe);

            TempData["message"] = "Welcome to ShelfMaster!";
            TempData["messageType"] = "success";
            return RedirectToAction("Index", "Dashboard");
        }

        // GET: Display 2FA code entry form
        [HttpGet]
        public IActionResult VerifyTwoFactorAuth()
        {
            if (HttpContext.Session.GetInt32("2FAUserId") == null)
            {
                return RedirectToAction("Login");
            }
            return View();
        }

        // POST: Validate 2FA code and sign in user
        [HttpPost]
        public async Task<IActionResult> VerifyTwoFactorAuth(string otpCode, bool rememberMe = false)
        {
            var userId = HttpContext.Session.GetInt32("2FAUserId");
            if (userId == null)
            {
                return RedirectToAction("Login");
            }

            var user = await _context.Users.FindAsync(userId.Value);
            if (user == null)
            {
                return RedirectToAction("Login");
            }

            if (string.IsNullOrWhiteSpace(otpCode))
            {
                ModelState.AddModelError("", "Please enter the verification code.");
                return View();
            }

            if (!_otpService.ValidateOtp(user.TwoFactorSecretKey!, otpCode))
            {
                ModelState.AddModelError("", "Invalid verification code.");
                return View();
            }

            // Clear temp session and sign in
            HttpContext.Session.Remove("2FAUserId");
            await SignInUserAsync(user, rememberMe);

            TempData["message"] = "Two-factor authentication successful.";
            TempData["messageType"] = "success";

            return RedirectToAction("Index", "Dashboard");
        }

        // Helper method to sign in user with cookie auth
        private async Task SignInUserAsync(User user, bool isPersistent = false)
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
                IsPersistent = isPersistent,
                ExpiresUtc = isPersistent ? DateTimeOffset.UtcNow.AddDays(7) : (DateTimeOffset?)null
            };

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                authProperties
            );

            // Send login notification email
            _ = _emailService.SendEmailAsync(user.Email, "Login Notification",
                $"<p>Your ShelfMaster account was successfully accessed on {DateTime.Now}. " +
                $"If this wasn’t you, please change your password immediately.</p>");
        }

        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login", "Auth");
        }

        // GET: Show 2FA setup page with QR code and manual key
        [HttpGet]
        public IActionResult Setup2FA()
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var user = _context.Users.Find(userId);
            if (user == null)
                return RedirectToAction("Login");

            if (string.IsNullOrEmpty(user.TwoFactorSecretKey))
            {
                var secretKey = KeyGeneration.GenerateRandomKey(20);
                user.TwoFactorSecretKey = Base32Encoding.ToString(secretKey);
                _context.SaveChanges();
            }

            string issuer = "ShelfMaster";
            string label = user.Email;
            string secret = user.TwoFactorSecretKey;

            var totpUri = $"otpauth://totp/{Uri.EscapeDataString(issuer)}:{Uri.EscapeDataString(label)}?secret={secret}&issuer={Uri.EscapeDataString(issuer)}";

            using var qrGenerator = new QRCoder.QRCodeGenerator();
            var qrCodeData = qrGenerator.CreateQrCode(totpUri, QRCoder.QRCodeGenerator.ECCLevel.Q);
            var qrCode = new QRCoder.PngByteQRCode(qrCodeData);
            var qrCodeBytes = qrCode.GetGraphic(20);
            var base64QrCode = Convert.ToBase64String(qrCodeBytes);

            ViewBag.QrCodeImage = $"data:image/png;base64,{base64QrCode}";
            ViewBag.ManualKey = user.TwoFactorSecretKey;

            return View();
        }

        // POST: Enable 2FA for user after verifying OTP code
        [HttpPost]
        public async Task<IActionResult> EnableTwoFactorAuth(string otpCode)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
                return RedirectToAction("Login");

            if (string.IsNullOrWhiteSpace(otpCode))
            {
                ModelState.AddModelError("", "Please enter the verification code.");
                return View("Setup2FA");
            }

            if (!_otpService.ValidateOtp(user.TwoFactorSecretKey!, otpCode))
            {
                ModelState.AddModelError("", "Invalid verification code.");
                return View("Setup2FA");
            }

            user.IsTwoFactorEnabled = true;
            await _context.SaveChangesAsync();

            TempData["message"] = "Two-factor authentication has been enabled.";
            TempData["messageType"] = "success";

            return RedirectToAction("Profile", "Account", new { id = user.Id });
        }

        [HttpPost]
        public IActionResult Verify2FA(string code)
        {
            int userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            var user = _context.Users.FirstOrDefault(u => u.Id == userId);

            if (user == null || string.IsNullOrEmpty(user.TwoFactorSecretKey))
            {
                TempData["message"] = "User or secret key not found.";
                TempData["messageType"] = "error";
                return RedirectToAction("Setup2FA");
            }

            var totp = new Totp(Base32Encoding.ToBytes(user.TwoFactorSecretKey));
            bool isValid = totp.VerifyTotp(code, out long timeStepMatched, new VerificationWindow(2, 2)); // Allows small clock drift

            if (isValid)
            {
                user.IsTwoFactorEnabled = true;
                _context.SaveChanges();

                TempData["message"] = "Two-factor authentication setup successful!";
                TempData["messageType"] = "success";
                return RedirectToAction("Profile", "Account", new { id = userId });
            }
            else
            {
                TempData["message"] = "Invalid code. Please try again.";
                TempData["messageType"] = "error";
                return RedirectToAction("Setup2FA");
            }
        }


        // GET: Register page
        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

        // POST: Handle user registration (basic example)
        [HttpPost]
        public async Task<IActionResult> Register([FromBody] RegisterViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { message = "Validation failed", errors = ModelState.ToDictionary() });
            }

            if (string.IsNullOrWhiteSpace(model.Password))
            {
                ModelState.AddModelError("Password", "Password is required.");
                return BadRequest(new { message = "Password is required", errors = ModelState.ToDictionary() });
            }

            if (model.Password.Length > 12)
            {
                ModelState.AddModelError("Password", "Password must be up to 12 characters long.");
                return BadRequest(new { message = "Password must be up to 12 characters long", errors = ModelState.ToDictionary() });
            }

            var userExists = await _context.Users.AnyAsync(u => u.Email == model.Email);
            if (userExists)
            {
                ModelState.AddModelError("Email", "Email already registered.");
                return BadRequest(new { message = "Email already registered", errors = ModelState.ToDictionary() });
            }

            var newUser = new User
            {
                Email = model.Email,
                Name = model.Name ?? "", // optional, or "" if not provided
                Password = _passwordHasher.HashPassword(null, model.Password),
                Role = "Borrower",
                IsVerified = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };

            _context.Users.Add(newUser);
            await _context.SaveChangesAsync();

            newUser.VerificationToken = Guid.NewGuid().ToString();
            await _context.SaveChangesAsync();

            var verificationLink = $"{Request.Scheme}://{Request.Host}/api/auth/verify?token={newUser.VerificationToken}";

            await _emailService.SendEmailAsync(newUser.Email, "Verify Your Email",
                $@"<p>Please verify your email by clicking the link below:</p>
           <p><a href='{verificationLink}'>Verify Email</a></p>");

            return Ok(new { message = "Registration successful! Please verify your email." });
        }

        // GET: Resend verification email form
        [HttpGet]
        public IActionResult ResendVerification()
        {
            return View();
        }

        // POST: Resend verification email
        [HttpPost]
        public async Task<IActionResult> ResendVerification(string email)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);

            if (user == null)
            {
                ModelState.AddModelError("email", "Email not found.");
                return View();
            }

            if ((bool)user.IsVerified)
            {
                ModelState.AddModelError("email", "Your email is already verified.");
                return View();
            }

            var verificationLink = $"{Request.Scheme}://{Request.Host}/api/auth/verify?token={user.VerificationToken}";

            await _emailService.SendEmailAsync(user.Email, "Verify Your Email",
                $@"<p>Dear {user.Name},</p>  
                <p>Please verify your email by clicking the link below:</p>  
                <p><a href='{verificationLink}' style='color: blue; font-weight: bold;'>Verify My Email</a></p>");

            TempData["message"] = "Verification email resent. Please check your inbox.";
            TempData["messageType"] = "success";

            return RedirectToAction("Login");
        }
    }
}
