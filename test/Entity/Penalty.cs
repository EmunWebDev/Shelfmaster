using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
namespace test.Entity
{
    public class Penalty
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey("Transaction")]
        public int TransactionID { get; set; } 
        public string? Reason { get; set; } // Overdue, Lost, Damaged

        [Column(TypeName = "decimal(10,2)")]
        [Range(0, double.MaxValue, ErrorMessage = "Fine amount must be positive.")]
        public decimal Amount { get; set; } 
        public bool IsPaid { get; set; } 
        public DateTime CreatedAt { get; set; }

        //parent
        public Transaction? Transaction { get; set; }
    }

}
