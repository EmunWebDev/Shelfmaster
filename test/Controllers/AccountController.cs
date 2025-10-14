using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

using test.Data;
using test.Models;
using test.Services;

namespace test.Controllers
{
    [Authorize(Roles = "Borrower")]
    public class AccountController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly EmailService _emailService;
        private readonly PasswordHasher<string> _passwordHasher = new PasswordHasher<string>();
        private readonly OtpService _otpService;

        public AccountController(ApplicationDbContext context, EmailService emailService, OtpService otpService)
        {
            _context = context;
            _emailService = emailService;
            _otpService = otpService;
        }

        [HttpGet]
        public IActionResult Profile(int id)
        {
            int userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));

            if (id != userId)
            {
                return Unauthorized();
            }

            var borrower = _context.Users
                .Where(u => u.Id == id)
                .Select(u => new BorrowerViewModel
                {
                    Id = u.Id,
                    Name = u.Name,
                    ContactNum = u.ContactNum,
                    BirthDate = u.BirthDate,
                    Gender = u.Gender,
                    Username = u.Username,
                    Email = u.Email,
                    IsVerified = u.IsVerified,
                    CreatedAt = u.CreatedAt
                })
                .FirstOrDefault();

            if (borrower == null)
            {
                return NotFound();
            }

            return View(borrower);
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
                user.IsVerified = false;
                user.VerificationToken = Guid.NewGuid().ToString();
                _context.SaveChanges();

                var verificationLink = $"{Request.Scheme}://{Request.Host}/api/auth/verify?token={user.VerificationToken}";

                await _emailService.SendEmailAsync(user.Email, "Verify Your Email",
                    $@"<p>Dear {user.Name},</p>  
                        <p>Your email has been updated. Please verify it to continue using your account.</p>  
                        <p><a href='{verificationLink}' style='color: blue; font-weight: bold;'>Verify My Email</a></p>  
                        <p>If you did not request this change, please contact support immediately.</p>  
                        <p>Best regards,</p>  
                        <p><strong>The ShelfMaster Support</strong></p>");

                TempData["message"] = "Your email has been updated. Please verify your new email.";
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

        [HttpPost]
        public async Task<IActionResult> Delete(int Id)
        {
            try
            {
                var borrower = await _context.Users.FindAsync(Id);
                if (borrower == null)
                {
                    return NotFound();
                }

                _context.Users.Remove(borrower);
                await _context.SaveChangesAsync();

                TempData["message"] = "Your account has been successfully deleted!";
                TempData["messageType"] = "success";

                await HttpContext.SignOutAsync();

                return RedirectToAction("Login", "Auth");
            }
            catch (Exception ex)
            {
                TempData["message"] = "An error occurred: " + ex.Message;
                TempData["messageType"] = "error";
                return RedirectToAction("Index");
            }
        }

        // GET: Show 2FA setup page
        [HttpGet]
        public IActionResult EnableTwoFactorAuth()
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            var user = _context.Users.Find(userId);
            if (user == null) return NotFound();

            if (!string.IsNullOrEmpty(user.TwoFactorSecretKey) && user.IsTwoFactorEnabled == true)
            {
                // Already setup, show QR code again if needed
                var (secret, qrUrl, manualKey) = _otpService.GenerateTotpSetup(user.Email, "YourAppName");
                var model = new TwoFactorSetupViewModel
                {
                    QrCodeUrl = qrUrl,
                    ManualKey = manualKey
                };
                return View(model);
            }

            // Generate new secret + QR code
            var (secretKey, qrCodeUrl, manualKeyNew) = _otpService.GenerateTotpSetup(user.Email, "YourAppName");
            TempData["SecretKey"] = secretKey;

            var newModel = new TwoFactorSetupViewModel
            {
                QrCodeUrl = qrCodeUrl,
                ManualKey = manualKeyNew
            };

            return View(newModel);
        }

        // POST: Verify and enable 2FA
        [HttpPost]
        public IActionResult EnableTwoFactorAuth(string otpCode)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            var user = _context.Users.Find(userId);
            if (user == null) return NotFound();

            var secretKey = TempData["SecretKey"] as string;
            if (string.IsNullOrEmpty(secretKey))
            {
                ModelState.AddModelError("", "Secret key missing, please try again.");
                return View();
            }

            if (_otpService.ValidateOtp(secretKey, otpCode))
            {
                user.TwoFactorSecretKey = secretKey;
                user.IsTwoFactorEnabled = true;
                _context.SaveChanges();

                TempData.Remove("SecretKey");
                TempData["message"] = "Two-factor authentication enabled successfully.";
                TempData["messageType"] = "success";

                return RedirectToAction("Profile", new { id = user.Id });
            }
            else
            {
                ModelState.AddModelError("", "Invalid OTP code.");
                var (secret, qrUrl, manual) = _otpService.GenerateTotpSetup(user.Email, "YourAppName");
                TempData["SecretKey"] = secret;

                var model = new TwoFactorSetupViewModel
                {
                    QrCodeUrl = qrUrl,
                    ManualKey = manual
                };

                return View(model);
            }
        }

    }
}
