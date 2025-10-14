using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace test.Entity
{
    public class Log
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey("User")]
        public int UserID { get; set; }

        [Required]
        [StringLength(100)]
        public required string Action { get; set; }

        [Required]
        [Column(TypeName = "NVARCHAR(MAX)")]
        public required string Details { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        //parent
        public User? User { get; set; }
    }
}
