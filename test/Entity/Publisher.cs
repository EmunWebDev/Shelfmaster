using System.ComponentModel.DataAnnotations;

namespace test.Entity
{
    public class Publisher
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public required string Name { get; set; }

        [Required]
        [StringLength(255)]
        public required string Address { get; set; }

        [Required]
        [StringLength(15)]
        public required string ContactNum { get; set; }

        [Required]
        [StringLength(100)]
        public required string Email { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        // child
        public ICollection<Book>? Book { get; set; }
    }
}
