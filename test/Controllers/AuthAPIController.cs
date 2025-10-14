using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using test.Areas.Admin.Models;
using test.Data;
using test.Entity;
using test.Enums;
using test.Services;

namespace test.Controllers
{
    [Route("api/auth")]
    [ApiController] 
    public class AuthApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly PasswordHasher<string> _passwordHasher = new PasswordHasher<string>();
        private readonly EmailService _emailService;

        public AuthApiController(ApplicationDbContext context, EmailService emailService)
        {
            _context = context;
            _emailService = emailService;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] BorrowerViewModel model)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState
                    .Where(m => m.Value.Errors.Count > 0)
                    .ToDictionary(
                        kvp => kvp.Key,
                        kvp => kvp.Value.Errors.Select(e => e.ErrorMessage).ToArray()
                    );

                return BadRequest(new { message = "Validation failed", errors });
            }

            //Check if email exists in Students table with Status = Active
            var student = await _context.Students
                .FirstOrDefaultAsync(s => s.Email == model.Email && s.Status == Enums.StudentStatus.Enrolled);

            if (student == null)
            {
                return BadRequest(new
                {
                    message = "Email is not associated with an active student.",
                    errors = new { Email = new[] { "Only active students can register." } }
                });
            }

            // Prevent duplicate registration in Users table
            bool emailExists = _context.Users.Any(u => u.Email == model.Email);
            if (emailExists)
            {
                return BadRequest(new
                {
                    message = "Email already registered.",
                    errors = new { Email = new[] { "Email already exists." } }
                });
            }

            // Generate Username
            string sanitizedUsername = model.Name.ToLower().Replace(" ", "");
            string generatedUsername = $"{sanitizedUsername}123";
            string hashedPassword = _passwordHasher.HashPassword(null, model.Password);

            var user = new User
            {
                Name = model.Name,
                Email = model.Email,
                IsVerified = false,
                VerificationToken = Guid.NewGuid().ToString(),
                Role = "Borrower",
                Username = generatedUsername,
                Password = hashedPassword,
                Status = UserStatus.Inactive,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            // Generate verification link
            var verificationLink = $"{Request.Scheme}://{Request.Host}/api/auth/verify?token={user.VerificationToken}";

            // Send email (same SMTP flow you already had)
            await _emailService.SendEmailAsync(user.Email, "Verify Your Email",
                $@"<p>Dear {user.Name},</p>  
        <p>Thank you for registering at <strong>ShelfMaster</strong>! Before you can access your account, we need to verify your email address.</p>  

        <p>Please click the link below to complete your email verification:</p>  
        <p><a href='{verificationLink}' style='color: blue; font-weight: bold;'>Verify My Email</a></p>  

        <p>If you did not sign up for a ShelfMaster account, please ignore this email.</p>  
        <p>Happy reading! 📚</p>  
        <p>Best regards,<br><strong>The ShelfMaster Support</strong></p>");

            return Ok(new { message = "User registered successfully! Please verify your email." });
        }

        [HttpGet("verify")]
        public async Task<IActionResult> VerifyEmail(string token)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.VerificationToken == token);
            if (user == null)
                return NotFound(new { message = "Invalid or expired token." });

            // Handle null safely
            bool isAlreadyVerified = user.IsVerified.HasValue && user.IsVerified.Value;

            if (isAlreadyVerified)
            {
                // Already verified, redirect with info message
                return RedirectToAction("Login", "Auth", new { verified = "already" });
            }

            // Mark user as verified
            user.IsVerified = true;
            user.VerificationToken = null;
            user.Status = UserStatus.Active;
            _context.Users.Update(user);
            await _context.SaveChangesAsync();

            //Send confirmation email
            await _emailService.SendEmailAsync(
                user.Email,
                "Registration Successful - ShelfMaster",
                $@"<p>Dear {user.Name},</p>
        <p>Your email has been successfully verified and your ShelfMaster account is now active.</p>
        <p>You can now log in and start exploring our library system.</p>
        <p><a href='{Request.Scheme}://{Request.Host}/auth/login' 
              style='padding: 10px 15px; background-color: #4A90E2; color: #fff; text-decoration: none; border-radius: 5px;'>
              Go to Login
           </a></p>
        <p>Welcome aboard! 📚</p>
        <p><strong>The ShelfMaster Support</strong></p>"
            );

            // Redirect to login with success message
            return RedirectToAction("Login", "Auth", new { verified = "success" });
        }

    }
}
