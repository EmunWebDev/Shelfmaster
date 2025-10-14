using System.ComponentModel.DataAnnotations;

namespace test.Entity
{
    public class Genre
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public required string Name { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
        // child
        public ICollection<Book>? Book { get; set; }
    }
}
