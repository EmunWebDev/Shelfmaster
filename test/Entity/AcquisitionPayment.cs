using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace test.Entity
{
    public class AcquisitionPayment
    {
        public int Id { get; set; }

        [Required]
        public int AcquisitionId { get; set; }
        public Acquisition Acquisition { get; set; }

        [Required]
        public int VendorId { get; set; }
        public Vendor Vendor { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        [Range(0.01, 999999999.99, ErrorMessage = "Amount must be greater than 0")]
        public decimal Amount { get; set; }

        [Required]
        [StringLength(50)]
        public string PaymentMethod { get; set; } = "Cash";

        [Required]
        public DateTime PaymentDate { get; set; } = DateTime.UtcNow;

        // If you don’t want a navigation to User, just store ID
        [Required]
        public int RecordedById { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }
}
