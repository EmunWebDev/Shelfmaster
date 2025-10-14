using System.ComponentModel.DataAnnotations;

namespace test.Entity
{
    public class Vendor
    {
        public int Id { get; set; }

        [Required]
        [StringLength(150)]
        public string Name { get; set; }

        [StringLength(250)]
        public string Address { get; set; }

        [Required, StringLength(100)]
        public string ContactPerson { get; set; }

        [Phone]
        [StringLength(20)]
        public string ContactNumber { get; set; }

        [EmailAddress]
        [StringLength(150)]
        public string Email { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        // Relationships
        public ICollection<Acquisition>? Acquisitions { get; set; }
    }
}
