using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace test.Entity
{
    public class Book
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(255)]
        public string? Title { get; set; }

        [Required]
        [Column(TypeName = "NVARCHAR(MAX)")]
        public string? Description { get; set; }

        [ForeignKey("Author")]
        public int AuthorID { get; set; }

        [ForeignKey("Publisher")]
        public int PublisherID { get; set; }

        [Required]
        [StringLength(20)]
        public string? ISBN { get; set; }

        [ForeignKey("Genre")]
        public int GenreID { get; set; }

        [Required]
        [Range(1000, 9999)]
        public int PublicationYear { get; set; }

        [StringLength(100)]
        public string? CoverImage { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;


        //parent
        public Author? Author { get; set; }
        public Publisher? Publisher { get; set; }
        public Genre? Genre { get; set; }


        // child
        public ICollection<BookCopy>? BookCopy { get; set; }

        public ICollection<Acquisition> Acquisitions { get; set; } = new List<Acquisition>();

        public bool IsObsolete { get; set; } = false;

    }
}
