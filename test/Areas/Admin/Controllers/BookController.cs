using Microsoft.AspNetCore.Mvc;
using X.PagedList.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using test.Data;
using test.Services;
using test.Areas.Admin.Models;
using test.Entity;
using QRCoder;

namespace test.Areas.Admin.Controllers
{
    [Authorize]
    [Area("Admin")]
    public class BookController : BaseController
    {
        private readonly ApplicationDbContext _context;
        private readonly LogService _logService;
        public BookController(ApplicationDbContext context, LogService logService)
        {
            _context = context;
           _logService = logService;
        }

        // BOOK CRUD

        [HttpGet]
        public IActionResult Index(string searchQuery, int? page)
        {
            int pageSize = 10;
            int pageNumber = page ?? 1;

            var books = _context.Books
                        .Include(b => b.Author)
                        .Include(b => b.Genre)
                        .Include(b => b.Publisher)
                        .Where(b => _context.BookCopies.Any(bc => bc.BookID == b.Id && bc.Status != "Archived"));

            if (!string.IsNullOrEmpty(searchQuery))
            {
                books = books.Where(b => b.ISBN.Contains(searchQuery) ||
                                         b.Author.Name.Contains(searchQuery) ||
                                         b.Genre.Name.Contains(searchQuery) ||
                                         b.Publisher.Name.Contains(searchQuery));
            }

            var pagedList = books.ToPagedList(pageNumber, pageSize);

            return View("Index", pagedList);
        }

        // populates the dropdowns
        [HttpGet]
        public IActionResult New()
        {
            var genres = _context.Genres.ToList();
            var authors = _context.Authors.ToList();
            var publishers = _context.Publishers.ToList();

            var viewModel = new BookViewModel
            {
                Genres = genres,
                Authors = authors,
                Publishers = publishers
            };
            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateBook(BookViewModel model, IFormFile CoverImage)
        {
            try
            {
                model.Genres = _context.Genres.ToList();
                model.Authors = _context.Authors.ToList();
                model.Publishers = _context.Publishers.ToList();

                if (!ModelState.IsValid)
                {
                    return View("New", model);
                }

                int GenreId = model.SelectedGenreId ?? 0;
                int AuthorId = model.SelectedAuthorId ?? 0;
                int PublisherId = model.SelectedPublisherId ?? 0;

                // Unique ISBN
                if (!string.IsNullOrWhiteSpace(model.ISBN) && _context.Books.Any(b => b.ISBN == model.ISBN))
                {
                    ModelState.AddModelError("ISBN", "A book with this ISBN already exists.");
                    return View("New", model);
                }

                if (GenreId == 0)
                {
                    ModelState.AddModelError("Genres", "Please select a genre.");
                    return View("New", model);
                }

                // Author
                if (model.SelectedAuthorId.HasValue)
                {
                    AuthorId = model.SelectedAuthorId.Value;
                }
                else if (!string.IsNullOrWhiteSpace(model.AuthorName))
                {
                    var newAuthor = new Author { Name = model.AuthorName };
                    _context.Authors.Add(newAuthor);
                    await _context.SaveChangesAsync();
                    AuthorId = newAuthor.Id;
                    _logService.LogAction(UserId, "New Author", $"{Username} created Author #{newAuthor.Id}.");
                }
                else
                {
                    ModelState.AddModelError("Authors", "Please select or enter an author.");
                    return View("New", model);
                }

                // Publisher
                if (model.SelectedPublisherId.HasValue)
                {
                    PublisherId = model.SelectedPublisherId.Value;
                }
                else if (!string.IsNullOrWhiteSpace(model.PublisherName))
                {
                    var newPublisher = new Entity.Publisher
                    {
                        Name = model.PublisherName,
                        Address = model.PublisherAddress,
                        ContactNum = model.PublisherContact,
                        Email = model.PublisherEmail
                    };
                    _context.Publishers.Add(newPublisher);
                    await _context.SaveChangesAsync();
                    PublisherId = newPublisher.Id;
                    _logService.LogAction(UserId, "New Publisher", $"{Username} created Publisher #{newPublisher.Id}.");
                }
                else
                {
                    ModelState.AddModelError("Publishers", "Please select or enter a publisher.");
                    return View("New", model);
                }

                // Cover image
                string coverImagePath = await UploadCoverImage(CoverImage, model.ISBN);
                if (coverImagePath == null)
                {
                    ModelState.AddModelError("CoverImage", "Unsuccessful upload.");
                    return View("New", model);
                }

                var book = new Book
                {
                    Title = model.Title,
                    Description = model.Description,
                    ISBN = model.ISBN,
                    PublicationYear = model.PublicationYear,
                    CoverImage = coverImagePath,
                    AuthorID = AuthorId,
                    PublisherID = PublisherId,
                    GenreID = GenreId
                };

                _context.Books.Add(book);
                await _context.SaveChangesAsync();
                _logService.LogAction(UserId, "New Book", $"{Username} created Book #{book.Id}.");

                int bookCount = _context.Books.Count();
                for (int i = 1; i <= model.BookCopies; i++)
                {
                    var copyNumber = $"C{bookCount}{i}";
                    var bookCopy = new BookCopy
                    {
                        BookID = book.Id,
                        CopyNumber = copyNumber,
                        Status = "Available",
                        CreatedAt = DateTime.UtcNow,
                        QrCodePath = GenerateQr(copyNumber)
                    };

                    _context.BookCopies.Add(bookCopy);
                }

                await _context.SaveChangesAsync();
                _logService.LogAction(UserId, "New Book Copies", $"{Username} added {model.BookCopies} copies for Book #{book.Id}.");

                TempData["message"] = "Book successfully added!";
                TempData["messageType"] = "success";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["message"] = "Error: " + ex.Message;
                TempData["messageType"] = "error";
                return RedirectToAction("Index");
            }
        }


        [HttpGet]
        public IActionResult Edit(int id)
        {
            var book = _context.Books
                        .Include(b => b.Author)
                        .Include(b => b.Genre)
                        .Include(b => b.Publisher)
                        .FirstOrDefault(b => b.Id == id);

            if (book == null)
            {
                return NotFound(); 
            }

            var model = new BookViewModel
            {
                Id = book.Id,
                Title = book.Title,
                Description = book.Description,
                ISBN = book.ISBN,
                PublicationYear = book.PublicationYear,
                SelectedAuthorId = book.AuthorID,
                SelectedGenreId = book.GenreID,
                SelectedPublisherId = book.PublisherID,
                CoverImageString = book.CoverImage,
                CreatedAt = book.CreatedAt,
                UpdatedAt = book.UpdatedAt,
                Authors = _context.Authors.ToList(),
                Genres = _context.Genres.ToList(),
                Publishers = _context.Publishers.ToList()
            };

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> Update(BookViewModel model)
        {
            try
            {
                var book = await _context.Books.FindAsync(model.Id);
                if (book == null)
                {
                    TempData["message"] = "Book not found.";
                    TempData["messageType"] = "error";
                    return RedirectToAction("Index");
                }

                book.Title = model.Title;
                book.Description = model.Description;
                book.PublicationYear = model.PublicationYear;
                book.AuthorID = model.SelectedAuthorId ?? book.AuthorID;
                book.PublisherID = model.SelectedPublisherId ?? book.PublisherID;
                book.GenreID = model.SelectedGenreId ?? book.GenreID;
                book.UpdatedAt = DateTime.Now;

                if (model.CoverImage != null)
                {
                    string newCoverImagePath = await UploadCoverImage(model.CoverImage, model.ISBN);

                    if (!string.IsNullOrEmpty(newCoverImagePath))
                    {
                        book.CoverImage = newCoverImagePath;
                    }
                    else
                    {
                        ModelState.AddModelError("CoverImage", "Failed to upload new cover image.");

                        return View(model);
                    }
                }

                _context.Books.Update(book);
                await _context.SaveChangesAsync();
                _logService.LogAction(UserId, "Update Book Information", $"{Username} updated the book #{book.Id}.");


                TempData["message"] = "Book updated successfully!";
                TempData["messageType"] = "success";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["message"] = "An error occurred: " + ex.Message;
                TempData["messageType"] = "error";
                return RedirectToAction("Index");
            }
        }


        private async Task<string> UploadCoverImage(IFormFile CoverImage, string isbn)
        {
            if (CoverImage == null || CoverImage.Length == 0)
            {
                return null;
            }

            var fileExtension = Path.GetExtension(CoverImage.FileName);
            string fileName = isbn + fileExtension;
            string uploadPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images/cover_images");

            if (!Directory.Exists(uploadPath))
            {
                Directory.CreateDirectory(uploadPath);
            }

            string filePath = Path.Combine(uploadPath, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await CoverImage.CopyToAsync(stream);
            }

            return Path.Combine("images/cover_images", fileName).Replace("\\", "/"); 
        }

        [HttpPost]
        public IActionResult MarkUnavailable(int id)
        {
            var book = _context.Books.Find(id);
            try
            {
                if (book == null)
                {
                    return NotFound();
                }

                // Find all copies of the book and update their status
                var bookCopies = _context.BookCopies.Where(bc => bc.BookID == id).ToList();
                if (bookCopies.Any())
                {
                    foreach (var copy in bookCopies)
                    {
                        copy.Status = "Archived";
                        copy.UpdatedAt = DateTime.Now;
                    }
                    _context.SaveChanges();
                    _logService.LogAction(UserId, "Mark Unavailable", $"{Username} marked the Book #{book.Id} as unavailable.");
                }

                TempData["message"] = "Book has been moved to archive. All copies are now unavailable.";
                TempData["messageType"] = "success";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["message"] = "An error occurred: " + ex.Message;
                TempData["messageType"] = "error";
                return RedirectToAction("Index");
            }
        }


        // Book Copies Management
        [HttpGet]
        public IActionResult ManageCopies(int id, int? page)
        {
            int pageSize = 5;
            int pageNumber = page ?? 1;

            var book = _context.Books
                .Include(b => b.BookCopy)
                .FirstOrDefault(b => b.Id == id);

            if (book == null)
            {
                return NotFound();
            }

            var model = new BookViewModel
            {
                Id = book.Id,
                Title = book.Title,
                Copies = book.BookCopy.Select(copy => new BookCopy
                {
                    Id = copy.Id,
                    CopyNumber = copy.CopyNumber,
                    Status = copy.Status,
                    QrCodePath = copy.QrCodePath,
                    CreatedAt = copy.CreatedAt,
                    UpdatedAt = copy.UpdatedAt,
                    ArchivedAt = copy.ArchivedAt,
                    ArchiveReason = copy.ArchiveReason
                }).ToPagedList(pageNumber, pageSize),
                BookCopies = book.BookCopy.Count
            };

            return View(model);
        }


        [HttpPost]
        public IActionResult AddCopies(int BookID, int NumberOfCopies)
        {
            var book = _context.Books.Include(b => b.BookCopy).FirstOrDefault(b => b.Id == BookID);
            try
            {
                if (book == null)
                {
                    TempData["error"] = "Book not found.";
                    return RedirectToAction("ManageCopies", new { id = BookID });
                }

                int bookCopyCount = _context.BookCopies.Count(b => b.BookID == BookID);

                for (int i = 1; i <= NumberOfCopies; i++)
                {
                    var copyNumber = $"C{BookID}{bookCopyCount + i}";

                    var newCopy = new BookCopy
                    {
                        BookID = BookID,
                        CopyNumber = copyNumber,
                        Status = "Available",
                        CreatedAt = DateTime.UtcNow,
                        QrCodePath = GenerateQr(copyNumber) 
                    };

                    _context.BookCopies.Add(newCopy);
                }

                _context.SaveChanges();
                TempData["message"] = $"{NumberOfCopies} book copies added successfully.";
                TempData["messageType"] = "success";
                return RedirectToAction("ManageCopies", new { id = BookID });
            }
            catch (Exception ex)
            {
                TempData["message"] = "Error: " + ex.Message;
                TempData["messageType"] = "error";
                return RedirectToAction("ManageCopies", new { id = book?.Id });
            }
        }

        [HttpPost]
        public async Task<IActionResult> UpdateBookCopyStatus(int id, string status, string? OtherStatus)
        {
            var bookCopy = await _context.BookCopies.FindAsync(id);

            if (bookCopy == null)
            {
                TempData["message"] = "Book Copy ID not found. ";
                TempData["messageType"] = "warning";
                return RedirectToAction("Index");
            }

            try
            { 

                // If "Other" is selected, use the provided custom status; otherwise, use the selected status
                bookCopy.Status = status == "Other" ? OtherStatus : status;
                bookCopy.UpdatedAt = DateTime.Now;

                _context.BookCopies.Update(bookCopy);
                await _context.SaveChangesAsync();
                _logService.LogAction(UserId, "Update Book Copy Status", $"{Username} updated the status of book copy '{bookCopy.CopyNumber}' (Book ID: {bookCopy.BookID}).");


                TempData["message"] = $"Book Copy #{bookCopy.CopyNumber} status updated successfully.";
                TempData["messageType"] = "success";
                return RedirectToAction("ManageCopies", new { id = bookCopy.BookID });
            }
            catch (Exception ex)
            {
                TempData["message"] = "An error occurred while processing your request: " + ex.Message;
                TempData["messageType"] = "error";
                return RedirectToAction("ManageCopies", new { id = bookCopy.BookID });
            }

        }


        // Author CRUD
        [HttpGet]
        public IActionResult AuthorList(string searchQuery, int? page)
        {
            int pageSize = 10;
            int pageNumber = (page ?? 1);

            var authors = _context.Authors.AsQueryable();

            if (!string.IsNullOrEmpty(searchQuery))
            {
                authors = authors.Where(a => a.Name.Contains(searchQuery));
            }
            var pagedList = authors.ToPagedList(pageNumber, pageSize);

            return View("~/Areas/Admin/Views/Book/Author/AuthorList.cshtml", pagedList);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateAuthor(string Name)
        {
            try
            {
                bool authorExists = await _context.Authors.AnyAsync(a => a.Name == Name);
                if (authorExists)
                {
                    TempData["message"] = $"An author with the name '{Name}' already exists.";
                    TempData["messageType"] = "error";
                    return RedirectToAction("AuthorList");
                }
                else
                {
                    var author = new Author
                    {
                        Name = Name
                    };

                    _context.Authors.Add(author);
                    _context.SaveChanges();
                    _logService.LogAction(UserId, "New Author", $"{Username} created a new author with ID #{author.Id}.");

                    TempData["message"] = "Author successfully added!";
                    TempData["messageType"] = "success";

                    return RedirectToAction("AuthorList");
                }
               
            }
            catch (Exception ex)
            {
                TempData["message"] = "An error occurred: " + ex.Message;
                TempData["messageType"] = "error";
                return RedirectToAction("AuthorList");
            }
           
        }

        [HttpPost]
        public async Task<IActionResult> EditAuthorAsync(int id, string name)
        {

            try
            {
                var author = await _context.Authors.FindAsync(id);
                if (author == null)
                {
                    return NotFound();
                }

                author.Name = name;
                author.UpdatedAt = DateTime.Now;
                _context.SaveChanges();
                _logService.LogAction(UserId, "Update Author's Information", $"{Username} updated the author's information with ID #{author.Id}.");

                TempData["Message"] = "Author updated successfully!";
                TempData["messageType"] = "success";
                return RedirectToAction("AuthorList");
            }
            catch (Exception ex)
            {
                TempData["message"] = "An error occurred: " + ex.Message;
                TempData["messageType"] = "error";
                return RedirectToAction("AuthorList");
            }
          
        }

        [HttpPost]
        public async Task<IActionResult> DeleteAuthor(int id)
        {
            try
            {
                var author = await _context.Authors.FindAsync(id);
                if (author == null)
                {
                    return NotFound();
                }

                _context.Authors.Remove(author);
                await _context.SaveChangesAsync();
                _logService.LogAction(UserId, "Delete Author", $"{Username} deleted the author with ID #{author.Id}.");

                TempData["message"] = "Author successfully deleted!";
                TempData["messageType"] = "success";
                return RedirectToAction("AuthorList");
            }
            catch (Exception ex)
            {
                TempData["message"] = "An error occurred: " + ex.Message;
                TempData["messageType"] = "error";
                return RedirectToAction("AuthorList");
            }
            
        }


        //Publisher CRUD
        [HttpGet]
        public IActionResult PublisherList(string searchQuery, int? page)
        {
            int pageSize = 10;
            int pageNumber = (page ?? 1);

            var publishers = _context.Publishers.AsQueryable();

            if (!string.IsNullOrEmpty(searchQuery))
            {
                publishers = publishers.Where(p => p.Name.Contains(searchQuery));
            }
            var pagedList = publishers.ToPagedList(pageNumber, pageSize);

            return View("~/Areas/Admin/Views/Book/Publisher/PublisherList.cshtml", pagedList);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreatePublisher(string Name, string Address, string ContactNum, string Email)
        {
            try
            {
                bool pubExists = await _context.Publishers.AnyAsync(p => p.Name == Name);
                if (pubExists)
                {
                    TempData["message"] = $"A publisher with the name '{Name}' already exists.";
                    TempData["messageType"] = "error";
                    return RedirectToAction("PublisherList");
                }
                var publisher = new Entity.Publisher
                {
                    Name = Name,
                    Address = Address,
                    ContactNum = ContactNum,
                    Email = Email
                };

                _context.Publishers.Add(publisher);
                _context.SaveChanges();
                _logService.LogAction(UserId, "New Publisher", $"{Username} created a new publisher with ID #{publisher.Id}.");

                TempData["message"] = "Publisher successfully added!";
                TempData["messageType"] = "success";

                return RedirectToAction("PublisherList");
            }
            catch (Exception ex)
            {
                TempData["message"] = "An error occurred: " + ex.Message;
                TempData["messageType"] = "error";
                return RedirectToAction("PublisherList");
            }
           
        }

        [HttpPost]
        public async Task<IActionResult> EditPublisherAsync(int id, string Name, string Address, string ContactNum, string Email)
        {
            try
            {
                var publisher = await _context.Publishers.FindAsync(id);
                if (publisher == null)
                {
                    return NotFound();
                }

                publisher.Name = Name;
                publisher.Address = Address;
                publisher.ContactNum = ContactNum;
                publisher.Email = Email;

                publisher.UpdatedAt = DateTime.Now;
                _context.SaveChanges();
                _logService.LogAction(UserId, "Update Publisher's Information", $"{Username} updated the publisher's information with ID #{publisher.Id}.");


                TempData["Message"] = "Publisher updated successfully!";
                TempData["messageType"] = "success";
                return RedirectToAction("PublisherList");
            }
            catch (Exception ex)
            {
                TempData["message"] = "An error occurred: " + ex.Message;
                TempData["messageType"] = "error";
                return RedirectToAction("PublisherList");
            }
           
        }

        [HttpPost]
        public async Task<IActionResult> DeletePublisher(int id)
        {
            try
            {
                var publisher = await _context.Publishers.FindAsync(id);
                if (publisher == null)
                {
                    return NotFound();
                }

                _context.Publishers.Remove(publisher);
                await _context.SaveChangesAsync();
                _logService.LogAction(UserId, "Delete Publisher", $"{Username} deleted the publisher with ID #{publisher.Id}.");

                TempData["message"] = "Publisher successfully deleted!";
                TempData["messageType"] = "success";
                return RedirectToAction("PublisherList");
            }
            catch (Exception ex)
            {
                TempData["message"] = "An error occurred: " + ex.Message;
                TempData["messageType"] = "error";
                return RedirectToAction("PublisherList");
            }
            
        }


        //Genre CRUD
        [HttpGet]
        public IActionResult GenreList(string searchQuery, int? page)
        {
            int pageSize = 10;
            int pageNumber = (page ?? 1);

            var genres = _context.Genres.AsQueryable();

            if (!string.IsNullOrEmpty(searchQuery))
            {
                genres = genres.Where(g => g.Name.Contains(searchQuery));
            }

            var pagedList = genres.ToPagedList(pageNumber, pageSize);

            return View("~/Areas/Admin/Views/Book/Genre/GenreList.cshtml", pagedList);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateGenre(string Name)
        {
            try
            {
                bool genreExists = await _context.Genres.AnyAsync(g => g.Name == Name);
                if (genreExists)
                {
                    TempData["message"] = $"A genre with the name '{Name}' already exists.";
                    TempData["messageType"] = "error";
                    return RedirectToAction("GenreList");
                }

                var genre = new Genre
                {
                    Name = Name,
                };

                _context.Genres.Add(genre);
                _context.SaveChanges();
                _logService.LogAction(UserId, "New Genre", $"{Username} created a new genre with ID #{genre.Id}.");

                TempData["message"] = "Genre successfully added!";
                TempData["messageType"] = "success";

                return RedirectToAction("GenreList");
            }
            catch (Exception ex)
            {
                TempData["message"] = "An error occurred: " + ex.Message;
                TempData["messageType"] = "error";
                return RedirectToAction("GenreList");
            }
           
        }

        [HttpPost]
        public async Task<IActionResult> EditGenreAsync(int id, string Name)
        {
            try
            {
                var genre = await _context.Genres.FindAsync(id);
                if (genre == null)
                {
                    return NotFound();
                }

                genre.Name = Name;
                genre.UpdatedAt = DateTime.Now;
                _context.SaveChanges();
                _logService.LogAction(UserId, "Update Genre's Information", $"{Username} updated the genre's information with ID #{genre.Id}.");

                TempData["Message"] = "Genre updated successfully!";
                TempData["messageType"] = "success";

                return RedirectToAction("GenreList");
            }
            catch (Exception ex)
            {
                TempData["message"] = "An error occurred: " + ex.Message;
                TempData["messageType"] = "error";
                return RedirectToAction("GenreList");
            }
          
        }

        [HttpPost]
        public async Task<IActionResult> DeleteGenre(int id)
        {
            try
            {
                var genre = await _context.Genres.FindAsync(id);
                if (genre == null)
                {
                    return NotFound();
                }

                _context.Genres.Remove(genre);
                await _context.SaveChangesAsync();
                _logService.LogAction(UserId, "Delete Genre", $"{Username} deleted the genre with ID #{genre.Id}.");

                TempData["Message"] = "Genre updated successfully!";
                TempData["messageType"] = "success";

                return RedirectToAction("GenreList");
            }
            catch (Exception ex)
            {
                TempData["message"] = "An error occurred: " + ex.Message;
                TempData["messageType"] = "error";
                return RedirectToAction("GenreList");
            }
           
        }


        //helper
        private string GenerateQr(string copyNumber)
        {
            using (var qrGenerator = new QRCodeGenerator())
            {
                QRCodeData qrCodeData = qrGenerator.CreateQrCode($"COPY-{copyNumber}", QRCodeGenerator.ECCLevel.Q);
                var qrCode = new PngByteQRCode(qrCodeData);
                byte[] qrCodeBytes = qrCode.GetGraphic(20);

                string folderPath = Path.Combine("wwwroot", "qrcodes");
                if (!Directory.Exists(folderPath))
                {
                    Directory.CreateDirectory(folderPath);
                }

                string filePath = Path.Combine(folderPath, $"{copyNumber}.png");
                System.IO.File.WriteAllBytes(filePath, qrCodeBytes);

                return $"/qrcodes/{copyNumber}.png";
            }
        }

        // ================================
        // ARCHIVE A COPY (NEW)
        // ================================
        [HttpPost]
        public async Task<IActionResult> ArchiveCopy(int id, string reason)
        {
            var bookCopy = await _context.BookCopies.FindAsync(id);
            if (bookCopy == null)
            {
                TempData["message"] = "Book Copy not found.";
                TempData["messageType"] = "error";
                return RedirectToAction("Index");
            }

            try
            {
                bookCopy.Status = "Archived";
                bookCopy.ArchiveReason = reason;
                bookCopy.ArchivedAt = DateTime.Now;
                bookCopy.UpdatedAt = DateTime.Now;

                _context.BookCopies.Update(bookCopy);
                await _context.SaveChangesAsync();

                _logService.LogAction(UserId, "Archive Copy",
                    $"{Username} archived copy '{bookCopy.CopyNumber}' of Book #{bookCopy.BookID} with reason: {reason}");

                TempData["message"] = $"Copy #{bookCopy.CopyNumber} has been archived.";
                TempData["messageType"] = "success";

                return RedirectToAction("ManageCopies", new { id = bookCopy.BookID });
            }
            catch (Exception ex)
            {
                TempData["message"] = "Error archiving copy: " + ex.Message;
                TempData["messageType"] = "error";
                return RedirectToAction("ManageCopies", new { id = bookCopy.BookID });
            }
        }

    }
}
