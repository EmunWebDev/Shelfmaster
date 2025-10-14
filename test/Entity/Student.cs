using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using test.Enums;

namespace test.Entity
{
    public class Student
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(20)]
        public string StudentNumber { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string LastName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [StringLength(255)]
        public string Email { get; set; } = string.Empty;

        [StringLength(20)]
        public string? ContactNumber { get; set; }

        [Required]
        [StringLength(100)]
        public string Department { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string Program { get; set; } = string.Empty;

        [Required]
        [StringLength(20)]
        public string YearLevel { get; set; } = string.Empty;

        [Required]
        [StringLength(10)]
        public string Section { get; set; } = string.Empty;

        public Gender Gender { get; set; }

        public StudentStatus Status { get; set; } = StudentStatus.Enrolled;

        [StringLength(500)]
        public string? Address { get; set; }

        public DateTime DateOfBirth { get; set; }

        public DateTime EnrollmentDate { get; set; } = DateTime.UtcNow;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
