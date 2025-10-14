using test.Entity;
using System.ComponentModel.DataAnnotations;
using X.PagedList;

namespace test.Areas.Admin.Models
{
    public class BorrowerViewModel
    {
        public int? Id { get; set; }

        [Required]
        [StringLength(100)]
        public string? Name { get; set; }

        public string? StudentNumber { get; set; }

        [Required]
        [EmailAddress]
        [StringLength(100, ErrorMessage = "Please enter a valid email.")]
        public required string Email { get; set; }

        [StringLength(255, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters.")]
        public string? Password { get; set; }

        [StringLength(100)]
        public string? Username { get; set; }

        [Phone]
        [StringLength(15)]
        public string? ContactNum { get; set; }

        public string? Department { get; set; }

        [DataType(DataType.Date)]
        public DateOnly? BirthDate { get; set; }

        [StringLength(10)]
        public string? Gender { get; set; }

        public string? Role { get; set; }

        public bool? IsVerified { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public ICollection<Transaction>? Transactions { get; set; }
    }
}
