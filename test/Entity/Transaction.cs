using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace test.Entity
{
    public class Transaction
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey("User")] 
        public int BorrowerID { get; set; }

        [ForeignKey("BookCopy")]
        public int BookCopyID { get; set; }

        public DateTime TransactionDate { get; set; } = DateTime.Now;

        public DateTime DueDate { get; set; }
        public DateTime? ReturnDate { get; set; }

        [StringLength(15)]
        public string? Status { get; set; } // Completed = returned in good condition, Active = still borrowed, Overdue, Lost, Damaged

        //parent
        public User? User { get; set; }
        public BookCopy? BookCopy { get; set; }

        // child
        public ICollection<Overdue>? Overdue { get; set; }
        public ICollection<Penalty>? Penalty { get; set; }
        public Payment Payment { get; set; }

    }
}
