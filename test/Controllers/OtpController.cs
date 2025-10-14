using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using test.Data;
using test.Services;

namespace test.Controllers
{
    public class OtpController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly OtpService _otpService;

        public OtpController(ApplicationDbContext context, OtpService otpService)
        {
            _context = context;
            _otpService = otpService;
        }

        [HttpGet]
        public IActionResult Setup()
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var user = _context.Users.Find(userId);

            var (secret, qrCode, manual) = _otpService.GenerateTotpSetup(user.Email, "ShelfMaster");

            user.TwoFactorSecretKey = secret;
            _context.SaveChanges();

            ViewBag.QrCode = qrCode;
            ViewBag.ManualKey = manual;

            return View();
        }

        [HttpPost]
        public IActionResult Verify(string code)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var user = _context.Users.Find(userId);

            if (_otpService.ValidateOtp(user.TwoFactorSecretKey, code))
            {
                user.IsTwoFactorEnabled = true;
                _context.SaveChanges();

                TempData["message"] = "2FA has been successfully enabled.";
                return RedirectToAction("Index", "Dashboard");
            }

            TempData["error"] = "Invalid OTP. Try again.";
            return RedirectToAction("Setup");
        }
    }
}
