using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace test.Entity
{
    public class BookCopy
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey("Book")]
        public int BookID { get; set; }

        [Required]
        public required string CopyNumber { get; set; }

        [Required]
        [StringLength(15)]
        public required string Status { get; set; } // Available, Borrowed, Overdue, Damaged, Lost, Archived
        
        [StringLength(255)]
        public string? QrCodePath { get; set; }

        public DateTime? ArchivedAt { get; set; }
        public string? ArchiveReason { get; set; }

        public bool IsDeleted { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
        //parent
        public Book? Book { get; set; }


        // child
        public ICollection<Transaction>? Transaction { get; set; }
    }
}
