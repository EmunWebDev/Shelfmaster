using test.Entity;
using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;
using X.PagedList;

namespace test.Areas.Admin.Models
{
    public class BookViewModel
    {
        public int Id { get; set; }

        [Required]
        public string? Title { get; set; }

        public string? Description { get; set; }

        [Required]
        public string? ISBN { get; set; }

        [Required]
        [Range(1900, 2099, ErrorMessage = "Year must be between 1900 and 2099")]
        public int PublicationYear { get; set; }

        public List<Genre>? Genres { get; set; }

        public int? SelectedGenreId { get; set; }

        public IFormFile? CoverImage { get; set; }
        public string? CoverImageString { get; set; }
        public List<Author>? Authors { get; set; }
        public int? SelectedAuthorId { get; set; }
        public string? AuthorName { get; set; }


        public List<Publisher>? Publishers { get; set; }
        public int? SelectedPublisherId { get; set; }

        public string? PublisherName { get; set; }
        public string? PublisherAddress { get; set; }
        public string? PublisherContact { get; set; }
        public string? PublisherEmail { get; set; }

        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "Total copies must be at least 1")]
        public int BookCopies { get; set; }

        public string? BookCopyStatus { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public IPagedList<BookCopy>? Copies { get; set; }
    }
}
