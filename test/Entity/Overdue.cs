using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace test.Entity
{
    public class Overdue
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey("Transaction")]
        public int TransactionID { get; set; }

        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "Overdue days must be at least 1.")]
        public int OverdueDays { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        [Range(0, double.MaxValue, ErrorMessage = "Fine amount must be positive.")]
        public decimal FineAmount { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;


        //parent
        public Transaction? Transaction { get; set; }

    }
}
