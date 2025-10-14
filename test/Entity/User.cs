using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using test.Enums;

namespace test.Entity
{
    public class User
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public required string Name { get; set; }

        [Phone]
        [StringLength(15)]
        public string? ContactNum { get; set; }

        public DateOnly? BirthDate { get; set; }

        [StringLength(10)]
        public string? Gender { get; set; }

        [Required]
        [StringLength(20)]
        public required string Role { get; set; }  // Admin, Librarian/Staff, Borrower (Students, Teachers)

        [StringLength(100)]
        public string? Username { get; set; }

        [Required]
        [EmailAddress]
        [StringLength(100, ErrorMessage = "Please enter a valid email.")]
        public required string Email { get; set; }

        public bool? IsVerified { get; set; } = false;

        public string? VerificationToken { get; set; } = string.Empty;

        [Required]
        [StringLength(255, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters.")]
        public required string Password { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        public UserStatus Status { get; set; } = UserStatus.Inactive;

        // 2FA-related fields
        [StringLength(128)]
        public string? TwoFactorSecretKey { get; set; }

        public bool IsTwoFactorEnabled { get; set; } = false;

        [StringLength(1024)]
        public string? TwoFactorRecoveryCodes { get; set; }  // Optional recovery codes for 2FA fallback

        public bool IsArchived { get; set; } = false;

        // Navigation properties
        public ICollection<Transaction>? Transaction { get; set; }
        public ICollection<Log>? Log { get; set; }
    }
}
