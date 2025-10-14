using System.ComponentModel.DataAnnotations;
using test.Enums;

namespace test.Entity
{
    public class Acquisition
    {

        public int Id { get; set; }

        // Vendor supplying the book
        [Required]
        public int VendorId { get; set; }
        public Vendor Vendor { get; set; }

        // Who requested (Librarian)
        [Required]
        public int RequestedById { get; set; }
        public User RequestedBy { get; set; }

        // Who approved (Admin)
        public int? ApprovedById { get; set; }
        public User ApprovedBy { get; set; }

        // --- Book metadata (to be catalogued later) ---
        [Required, StringLength(255)]
        public string Title { get; set; }

        [StringLength(20)]
        public string ISBN { get; set; }

        [Range(1000, 2100)]
        public int? PublicationYear { get; set; }

        // Author
        public int? TempAuthorId { get; set; }
        public Author TempAuthor { get; set; }

        [StringLength(100)]
        public string AuthorName { get; set; }   // fallback if new

        // Publisher
        public int? TempPublisherId { get; set; }
        public Publisher TempPublisher { get; set; }

        [StringLength(100)]
        public string PublisherName { get; set; } // fallback if new

        // Genre
        public int? TempGenreId { get; set; }
        public Genre TempGenre { get; set; }

        [StringLength(100)]
        public string GenreName { get; set; }     // fallback if new

        // Request details
        [Required]
        [Range(1, 1000)]
        public int Quantity { get; set; }

        [Range(0, 999999.99)]
        public decimal? TotalCost { get; set; }

        [StringLength(500)]
        public string Notes { get; set; }

        // Workflow
        public AcquisitionStatus Status { get; set; } = AcquisitionStatus.Requested;
        public virtual ICollection<AcquisitionPayment> AcquisitionPayments { get; set; } = new List<AcquisitionPayment>();


        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public DateTime? InspectedAt { get; set; }
        public DateTime? CataloguedAt { get; set; }

        public int? BookId { get; set; }
        public Book? Book { get; set; }
    }
}
