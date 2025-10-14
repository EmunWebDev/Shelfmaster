using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace test.Areas.Admin.Models
{
    public class AcquisitionViewModel
    {
        [Required(ErrorMessage = "Please select a vendor.")]
        public int VendorId { get; set; }
        public IEnumerable<SelectListItem>? Vendors { get; set; }

        [Required(ErrorMessage = "Title is required")]
        public string Title { get; set; }

        public string? ISBN { get; set; }

        [Range(1000, 2100, ErrorMessage = "Publication Year must be between 1000 and 2100")]
        public int? PublicationYear { get; set; }

        // Author - either select existing or add new
        public int SelectedAuthorId { get; set; }
        public IEnumerable<SelectListItem>? Authors { get; set; }
        public string? AuthorName { get; set; }   // optional unless no dropdown selected

        // Publisher - either select existing or add new
        public int SelectedPublisherId { get; set; }
        public IEnumerable<SelectListItem>? Publishers { get; set; }
        public string? PublisherName { get; set; }   // optional unless no dropdown selected
        public string? PublisherAddress { get; set; }
        public string? PublisherContact { get; set; }
        public string? PublisherEmail { get; set; }

        // Genre - either select existing or add new
        public int SelectedGenreId { get; set; }
        public IEnumerable<SelectListItem>? Genres { get; set; }
        public string? GenreName { get; set; }   // optional unless no dropdown selected

        [Required(ErrorMessage = "Quantity is required")]
        [Range(1, int.MaxValue, ErrorMessage = "Quantity must be at least 1")]
        public int Quantity { get; set; }

        public decimal? EstimatedCost { get; set; }

        public string? Notes { get; set; }
    }
}
