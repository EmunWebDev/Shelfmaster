using test.Entity;
using System.ComponentModel.DataAnnotations;

namespace test.Models
{
    public class BorrowerViewModel
    {
        public int? Id { get; set; }

        [StringLength(100)]
        public string? Name { get; set; }

        [Phone]
        [StringLength(15)]
        public string? ContactNum { get; set; }

        [DataType(DataType.Date)]
        public DateOnly? BirthDate { get; set; }

        [StringLength(10)]
        public string? Gender { get; set; }

        [StringLength(10)]
        public string? Role { get; set; }

        [StringLength(255)]
        public string? Username { get; set; }

        [Required]
        [EmailAddress]
        [StringLength(100, ErrorMessage = "Please enter a valid email.")]
        public required string Email { get; set; }

        [Required]
        [StringLength(255, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters.")]
        [RegularExpression(@"^(?=.*[A-Z])(?=.*\d)(?=.*[\W_]).{8,}$",
        ErrorMessage = "Password must contain at least one uppercase letter, one number, and one special character.")]
        public string? Password { get; set; }

        public bool RememberMe { get; set; } = false;
        public bool? IsVerified { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        // TRANSACTIONS
        public ICollection<Transaction>? Transactions { get; set; }

        // ========================
        // 🔐 OTP / 2FA Fields
        // ========================

        /// <summary>
        /// Is TOTP-based 2FA enabled for this user?
        /// </summary>
        public bool IsTwoFactorEnabled { get; set; } = false;

        /// <summary>
        /// Used to input the 6-digit OTP from the authenticator app
        /// </summary>
        [Display(Name = "OTP Code")]
        [StringLength(6, ErrorMessage = "OTP must be 6 digits.")]
        public string? OTPCode { get; set; }

        /// <summary>
        /// Optional: Show QR code for setup in view
        /// </summary>
        public string? TotpQrCodeUrl { get; set; }

        /// <summary>
        /// Optional: For displaying a secret to scan manually if needed
        /// </summary>
        public string? TotpManualKey { get; set; }
    }
}
