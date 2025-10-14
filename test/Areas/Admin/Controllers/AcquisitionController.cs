using MailKit.Search;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Exchange.WebServices.Data;
using QRCoder;
using System.Drawing;
using System.Drawing.Imaging;
using System.Security.Claims;
using test.Areas.Admin.Models;
using test.Data;
using test.Entity;
using test.Enums;
using test.Services;
using X.PagedList;
using X.PagedList.Extensions;

namespace test.Areas.Admin.Controllers
{
    [Authorize]
    [Area("Admin")]
    public class AcquisitionController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly LogService _logService;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public AcquisitionController(ApplicationDbContext context, LogService logService, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _logService = logService;
            _webHostEnvironment = webHostEnvironment;
        }

        // -------------------------
        // LIST ACQUISITIONS - Different views for Admin vs Librarian
        // -------------------------
        [HttpGet]
        public IActionResult Index(string searchQuery, int? page)
        {
            var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
            var isAdmin = userRole == "Admin";
            ViewBag.IsAdmin = isAdmin;

            int pageSize = 3;
            int pageNumber = page ?? 1;

            var acquisitions = _context.Acquisitions
                .Include(a => a.Vendor)
                .Include(a => a.RequestedBy)
                .Include(a => a.ApprovedBy)
                .Include(a => a.Book)
                .AsQueryable();

            if (!string.IsNullOrEmpty(searchQuery))
            {
                acquisitions = acquisitions.Where(a =>
                    a.Title.Contains(searchQuery) ||
                    a.ISBN.Contains(searchQuery) ||
                    a.Vendor.Name.Contains(searchQuery) ||
                    a.AuthorName.Contains(searchQuery) ||
                    a.PublisherName.Contains(searchQuery) ||
                    a.GenreName.Contains(searchQuery));
            }

            var pagedList = acquisitions
                .OrderByDescending(a => a.CreatedAt)
                .ToPagedList(pageNumber, pageSize);

            return View("Index", pagedList);
        }


        // -------------------------
        // NEW REQUEST (LIBRARIAN) - POPULATE ALL DROPDOWNS
        // -------------------------
        [HttpGet]
        public IActionResult New()
        {
            var model = new AcquisitionViewModel
            {
                Vendors = _context.Vendors
                    .Select(v => new SelectListItem
                    {
                        Value = v.Id.ToString(),
                        Text = v.Name
                    })
                    .ToList(),
                Authors = _context.Authors
                    .Select(a => new SelectListItem
                    {
                        Value = a.Id.ToString(),
                        Text = a.Name
                    })
                    .ToList(),
                Publishers = _context.Publishers
                    .Select(p => new SelectListItem
                    {
                        Value = p.Id.ToString(),
                        Text = p.Name
                    })
                    .ToList(),
                Genres = _context.Genres
                    .Select(g => new SelectListItem
                    {
                        Value = g.Id.ToString(),
                        Text = g.Name
                    })
                    .ToList()
            };

            return View(model);
        }

        // -------------------------
        // CREATE REQUEST - Handle both existing selections and new entries
        // -------------------------
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(AcquisitionViewModel model)
        {
            // Vendor must be chosen
            if (model.VendorId == 0)
                ModelState.AddModelError("VendorId", "Please select a vendor.");

            // Author validation
            if (model.SelectedAuthorId == 0 && string.IsNullOrWhiteSpace(model.AuthorName))
                ModelState.AddModelError("AuthorName", "Please select an existing author OR enter a new author name.");

            // Publisher validation
            if (model.SelectedPublisherId == 0 && string.IsNullOrWhiteSpace(model.PublisherName))
                ModelState.AddModelError("PublisherName", "Please select an existing publisher OR enter a new publisher name.");

            // If entering a NEW publisher → require details
            if (model.SelectedPublisherId == 0 && !string.IsNullOrWhiteSpace(model.PublisherName))
            {
                if (string.IsNullOrWhiteSpace(model.PublisherAddress))
                    ModelState.AddModelError("PublisherAddress", "Publisher address is required for a new publisher.");

                if (string.IsNullOrWhiteSpace(model.PublisherContact))
                    ModelState.AddModelError("PublisherContact", "Publisher contact is required for a new publisher.");

                if (string.IsNullOrWhiteSpace(model.PublisherEmail))
                    ModelState.AddModelError("PublisherEmail", "Publisher email is required for a new publisher.");
            }

            // Genre validation
            if (model.SelectedGenreId == 0 && string.IsNullOrWhiteSpace(model.GenreName))
                ModelState.AddModelError("GenreName", "Please select an existing genre OR enter a new genre name.");

            if (!ModelState.IsValid)
            {
                await PopulateDropdowns(model);
                return View("New", model);
            }

            // Vendor lookup
            var vendor = await _context.Vendors.FindAsync(model.VendorId);
            if (vendor == null)
            {
                ModelState.AddModelError("VendorId", "Selected vendor not found.");
                await PopulateDropdowns(model);
                return View("New", model);
            }

            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "1");

            // Normalize values
            string authorName = model.SelectedAuthorId > 0
                ? _context.Authors.Find(model.SelectedAuthorId)?.Name ?? model.AuthorName!
                : model.AuthorName!;

            string publisherName = model.SelectedPublisherId > 0
                ? _context.Publishers.Find(model.SelectedPublisherId)?.Name ?? model.PublisherName!
                : model.PublisherName!;

            string genreName = model.SelectedGenreId > 0
                ? _context.Genres.Find(model.SelectedGenreId)?.Name ?? model.GenreName!
                : model.GenreName!;

            var acquisition = new Acquisition
            {
                VendorId = model.VendorId,
                Title = model.Title,
                ISBN = model.ISBN,
                PublicationYear = model.PublicationYear,
                Quantity = model.Quantity,
                TotalCost = model.EstimatedCost,
                Notes = model.Notes,
                RequestedById = userId,
                Status = AcquisitionStatus.Requested,

                // Store IDs if existing entities were selected
                TempAuthorId = model.SelectedAuthorId > 0 ? model.SelectedAuthorId : null,
                TempPublisherId = model.SelectedPublisherId > 0 ? model.SelectedPublisherId : null,
                TempGenreId = model.SelectedGenreId > 0 ? model.SelectedGenreId : null,

                // Store names as fallback
                AuthorName = authorName?.Trim(),
                PublisherName = publisherName?.Trim(),
                GenreName = genreName?.Trim(),

                CreatedAt = DateTime.UtcNow
            };

            _context.Acquisitions.Add(acquisition);
            await _context.SaveChangesAsync();

            TempData["message"] = "Acquisition request submitted successfully!";
            TempData["messageType"] = "success";
            return RedirectToAction("Index");
        }


        // Helper method to populate all dropdowns
        private async System.Threading.Tasks.Task PopulateDropdowns(AcquisitionViewModel model)
        {
            model.Vendors = _context.Vendors
                .Select(v => new SelectListItem { Value = v.Id.ToString(), Text = v.Name })
                .ToList();

            model.Authors = _context.Authors
                .Select(a => new SelectListItem { Value = a.Id.ToString(), Text = a.Name })
                .ToList();

            model.Publishers = _context.Publishers
                .Select(p => new SelectListItem { Value = p.Id.ToString(), Text = p.Name })
                .ToList();

            model.Genres = _context.Genres
                .Select(g => new SelectListItem { Value = g.Id.ToString(), Text = g.Name })
                .ToList();
        }

        // -------------------------
        // ADMIN ONLY - APPROVE / REJECT
        // -------------------------
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Approve(int id)
        {
            var acq = await _context.Acquisitions
                .Include(a => a.Vendor)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (acq == null) return NotFound();

            if (acq.Status != AcquisitionStatus.Requested)
            {
                TempData["message"] = "Only requested acquisitions can be approved.";
                TempData["messageType"] = "error";
                return RedirectToAction("Index");
            }

            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "1");
            var username = User.Identity?.Name ?? "Unknown";

            // Approve acquisition
            acq.Status = AcquisitionStatus.Approved;
            acq.ApprovedById = userId;
            acq.ApprovedAt = DateTime.UtcNow;
            acq.UpdatedAt = DateTime.UtcNow;

            _context.Update(acq);
            await _context.SaveChangesAsync();

            // Log approval
            _logService.LogAction(
                userId,
                "Acquisition Approval",
                $"{username} approved Acquisition #{acq.Id}."
            );

            //Auto-create vendor payment
            var payment = new AcquisitionPayment
            {
                AcquisitionId = acq.Id,
                VendorId = acq.VendorId,
                Amount = acq.TotalCost ?? 0,
                PaymentMethod = "Auto (Approval)",
                PaymentDate = DateTime.UtcNow,
                RecordedById = userId,
                Notes = "Automatically paid upon approval",
                CreatedAt = DateTime.UtcNow
            };

            _context.AcquisitionPayments.Add(payment);
            await _context.SaveChangesAsync();

            // Log payment
            _logService.LogAction(
                userId,
                "Vendor Payment",
                $"{username} auto-recorded a vendor payment of ₱{payment.Amount:N2} to {acq.Vendor?.Name} for Acquisition #{acq.Id} (on approval)."
            );

            TempData["message"] = "Acquisition approved and vendor payment recorded successfully.";
            TempData["messageType"] = "success";
            return RedirectToAction("Index");
        }




        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Reject(int id, string notes)
        {
            var acq = await _context.Acquisitions.FindAsync(id);
            if (acq == null) return NotFound();

            if (acq.Status != AcquisitionStatus.Requested)
            {
                TempData["message"] = "Only requested acquisitions can be rejected.";
                TempData["messageType"] = "error";
                return RedirectToAction("Index");
            }

            acq.Status = AcquisitionStatus.Rejected;
            acq.Notes = string.IsNullOrEmpty(acq.Notes) ? notes : $"{acq.Notes}\n\nRejection Notes: {notes}";
            acq.UpdatedAt = DateTime.UtcNow;

            _context.Update(acq);
            await _context.SaveChangesAsync();

            //Log rejection
            _logService.LogAction(
                int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0"),
                "Acquisition Rejected",
                $"{User.Identity?.Name ?? "Unknown"} rejected Acquisition #{acq.Id} with notes: {notes}"
            );

            TempData["message"] = "Acquisition rejected.";
            TempData["messageType"] = "warning";
            return RedirectToAction("Index");
        }


        // -------------------------
        // DELIVERY + INSPECTION - Admin Only
        // -------------------------
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> MarkDelivered(int id)
        {
            var acq = await _context.Acquisitions.FindAsync(id);
            if (acq == null) return NotFound();

            if (acq.Status != AcquisitionStatus.Approved)
            {
                TempData["message"] = "Only approved acquisitions can be marked as delivered.";
                TempData["messageType"] = "error";
                return RedirectToAction("Index");
            }

            acq.Status = AcquisitionStatus.Delivered;
            acq.UpdatedAt = DateTime.UtcNow;

            _context.Update(acq);
            await _context.SaveChangesAsync();

            //Log delivery
            _logService.LogAction(
                int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0"),
                "Acquisition Delivered",
                $"{User.Identity?.Name ?? "Unknown"} marked Acquisition #{acq.Id} as delivered."
            );

            TempData["message"] = "Marked as delivered successfully.";
            TempData["messageType"] = "success";
            return RedirectToAction("Index");
        }


        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Inspect(int id)
        {
            var acq = await _context.Acquisitions
                .Include(a => a.Vendor)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (acq == null) return NotFound();

            if (acq.Status != AcquisitionStatus.Delivered)
            {
                TempData["message"] = "Only delivered acquisitions can be inspected.";
                TempData["messageType"] = "error";
                return RedirectToAction("Index");
            }

            try
            {
                string Normalize(string? value) => (value ?? "").Trim().ToLowerInvariant();

                // -------------------
                // Handle Author
                // -------------------
                if (!acq.TempAuthorId.HasValue && !string.IsNullOrWhiteSpace(acq.AuthorName))
                {
                    var normalized = Normalize(acq.AuthorName);
                    var author = await _context.Authors
                        .FirstOrDefaultAsync(a => a.Name.ToLower() == normalized);

                    if (author == null)
                    {
                        author = new Author
                        {
                            Name = acq.AuthorName.Trim(),
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        };
                        _context.Authors.Add(author);
                        await _context.SaveChangesAsync();
                    }
                    acq.TempAuthorId = author.Id;
                }

                // -------------------
                // Handle Publisher
                // -------------------
                if (!acq.TempPublisherId.HasValue && !string.IsNullOrWhiteSpace(acq.PublisherName))
                {
                    var normalized = Normalize(acq.PublisherName);
                    var publisher = await _context.Publishers
                        .FirstOrDefaultAsync(p => p.Name.ToLower() == normalized);

                    if (publisher == null)
                    {
                        publisher = new Publisher
                        {
                            Name = acq.PublisherName.Trim(),
                            Address = "To be updated",
                            ContactNum = "To be updated",
                            Email = "tbd@example.com",
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        };
                        _context.Publishers.Add(publisher);
                        await _context.SaveChangesAsync();
                    }
                    acq.TempPublisherId = publisher.Id;
                }

                // -------------------
                // Handle Genre
                // -------------------
                if (!acq.TempGenreId.HasValue && !string.IsNullOrWhiteSpace(acq.GenreName))
                {
                    var normalized = Normalize(acq.GenreName);
                    var genre = await _context.Genres
                        .FirstOrDefaultAsync(g => g.Name.ToLower() == normalized);

                    if (genre == null)
                    {
                        genre = new Genre
                        {
                            Name = acq.GenreName.Trim(),
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        };
                        _context.Genres.Add(genre);
                        await _context.SaveChangesAsync();
                    }
                    acq.TempGenreId = genre.Id;
                }

                // -------------------
                // Mark as Inspected
                // -------------------
                acq.Status = AcquisitionStatus.Checked;
                acq.InspectedAt = DateTime.UtcNow;
                acq.UpdatedAt = DateTime.UtcNow;

                _context.Update(acq);
                await _context.SaveChangesAsync();

                // -------------------
                // Auto-create Vendor Payment
                // -------------------
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
                var username = User.Identity?.Name ?? "Unknown";

                var payment = new AcquisitionPayment
                {
                    AcquisitionId = acq.Id,
                    VendorId = acq.VendorId,
                    Amount = acq.TotalCost ?? 0,
                    PaymentMethod = "Auto (Inspection)",
                    PaymentDate = DateTime.UtcNow,
                    RecordedById = userId,
                    Notes = "Automatically paid upon inspection",
                    CreatedAt = DateTime.UtcNow
                };

                _context.AcquisitionPayments.Add(payment);
                await _context.SaveChangesAsync();

                // -------------------
                // Logging
                // -------------------
                _logService.LogAction(
                    userId,
                    "Acquisition Inspection",
                    $"{username} inspected Acquisition #{acq.Id}."
                );

                _logService.LogAction(
                    userId,
                    "Vendor Payment",
                    $"{username} auto-recorded a vendor payment of ₱{payment.Amount:N2} to {acq.Vendor?.Name} for Acquisition #{acq.Id}."
                );

                TempData["message"] = "Inspection complete and vendor payment recorded successfully.";
                TempData["messageType"] = "success";
            }
            catch (Exception ex)
            {
                TempData["message"] = $"Error during inspection: {ex.Message}";
                TempData["messageType"] = "error";
            }

            return RedirectToAction("Index");
        }




        // -------------------------
        // CATALOG (create Book + Copies with QR) - Admin Only
        // -------------------------
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Catalog(int id)
        {
            var acq = await _context.Acquisitions
                .Include(a => a.TempAuthor)
                .Include(a => a.TempPublisher)
                .Include(a => a.TempGenre)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (acq == null) return NotFound();

            if (acq.Status != AcquisitionStatus.Checked)
            {
                TempData["message"] = "Acquisition must be inspected first before cataloguing.";
                TempData["messageType"] = "error";
                return RedirectToAction("Index");
            }

            if (!acq.TempAuthorId.HasValue || !acq.TempPublisherId.HasValue || !acq.TempGenreId.HasValue)
            {
                TempData["message"] = "Author, Publisher, and Genre must be created during inspection.";
                TempData["messageType"] = "error";
                return RedirectToAction("Index");
            }

            try
            {
                // Create Book
                var book = new Book
                {
                    Title = acq.Title,
                    ISBN = acq.ISBN,
                    AuthorID = acq.TempAuthorId.Value,
                    PublisherID = acq.TempPublisherId.Value,
                    GenreID = acq.TempGenreId.Value,
                    PublicationYear = acq.PublicationYear ?? DateTime.UtcNow.Year,
                    Description = string.IsNullOrEmpty(acq.Notes) ? "No description provided." : acq.Notes,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.Books.Add(book);
                await _context.SaveChangesAsync();

                // Create Book Copies with QR codes (similar to BookController approach)
                for (int i = 1; i <= acq.Quantity; i++)
                {
                    var copyNumber = $"ACQ{acq.Id:D6}-C{i:D3}";
                    var qrCodePath = GenerateQr(copyNumber);

                    var copy = new BookCopy
                    {
                        BookID = book.Id,
                        CopyNumber = copyNumber,
                        Status = "Available",
                        QrCodePath = qrCodePath,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    _context.BookCopies.Add(copy);
                }

                // Update Acquisition
                acq.BookId = book.Id;
                acq.Status = AcquisitionStatus.Catalogued;
                acq.CataloguedAt = DateTime.UtcNow;
                acq.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                TempData["message"] = $"Acquisition catalogued successfully! Created {acq.Quantity} book copies with QR codes.";
                TempData["messageType"] = "success";
            }
            catch (Exception ex)
            {
                TempData["message"] = $"Error during cataloguing: {ex.Message}";
                TempData["messageType"] = "error";
            }

            return RedirectToAction("Index");
        }

        // -------------------------
        // QR CODE GENERATION (matching BookController approach)
        // -------------------------
        private string GenerateQr(string copyNumber)
        {
            using (var qrGenerator = new QRCodeGenerator())
            {
                QRCodeData qrCodeData = qrGenerator.CreateQrCode($"COPY-{copyNumber}", QRCodeGenerator.ECCLevel.Q);
                var qrCode = new PngByteQRCode(qrCodeData);
                byte[] qrCodeBytes = qrCode.GetGraphic(20);

                string wwwRootPath = _webHostEnvironment.WebRootPath;
                string qrFolderPath = Path.Combine(wwwRootPath, "qrcodes");

                if (!Directory.Exists(qrFolderPath))
                {
                    Directory.CreateDirectory(qrFolderPath); // Might throw if no write permission
                }

                string filePath = Path.Combine(qrFolderPath, $"{copyNumber}.png");
                System.IO.File.WriteAllBytes(filePath, qrCodeBytes);

                return $"/qrcodes/{copyNumber}.png";
            }
        }

        // -------------------------
        // DETAILS VIEW - Read Only for Librarians

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var acquisition = await _context.Acquisitions
                .Include(a => a.Vendor)
                .Include(a => a.RequestedBy)
                .Include(a => a.ApprovedBy)
                .Include(a => a.Book).ThenInclude(b => b.BookCopy)
                .Include(a => a.TempAuthor)
                .Include(a => a.TempPublisher)
                .Include(a => a.TempGenre)
                .Include(a => a.AcquisitionPayments)   
                .FirstOrDefaultAsync(a => a.Id == id);

            if (acquisition == null)
                return NotFound();

            var vm = new AcquisitionDetailsViewModel
            {
                Acquisition = acquisition,
                AcquisitionPayments = acquisition.AcquisitionPayments?.ToList() ?? new List<AcquisitionPayment>()  
            };

            return View(vm);
        }


        // -------------------------
        // GET STATUS BADGE CSS CLASS
        // -------------------------
        public static string GetStatusBadgeClass(AcquisitionStatus status)
        {
            return status switch
            {
                AcquisitionStatus.Requested => "badge bg-warning",
                AcquisitionStatus.Approved => "badge bg-info",
                AcquisitionStatus.Rejected => "badge bg-danger",
                AcquisitionStatus.Delivered => "badge bg-primary",
                AcquisitionStatus.Checked => "badge bg-secondary",
                AcquisitionStatus.Catalogued => "badge bg-success",
                _ => "badge bg-secondary"
            };
        }

        // -------------------------
        // ADD PAYMENT (using Payment entity)
        // -------------------------
        [HttpGet]
        public async Task<IActionResult> AddPayment(int acquisitionId)
        {
            var acquisition = await _context.Acquisitions.Include(a => a.Vendor).FirstOrDefaultAsync(a => a.Id == acquisitionId);
            if (acquisition == null) return NotFound();

            var existingPayments = await _context.Payments
                .Where(p => p.TransactionID == acquisitionId)
                .OrderByDescending(p => p.PaymentDate)
                .ToListAsync();

            var viewModel = new AcquisitionPaymentViewModel
            {
                AcquisitionId = acquisition.Id,
                VendorName = acquisition.Vendor?.Name,
                ExistingPayments = existingPayments,
                NewPayment = new Payment
                {
                    TransactionID = acquisition.Id,
                    PaymentDate = DateTime.UtcNow
                }
            };

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddPayment(AcquisitionPaymentViewModel model)
        {
            if (!ModelState.IsValid)
            {
                model.ExistingPayments = await _context.Payments
                    .Where(p => p.TransactionID == model.AcquisitionId)
                    .OrderByDescending(p => p.PaymentDate)
                    .ToListAsync();
                return View(model);
            }

            var acquisition = await _context.Acquisitions.FindAsync(model.AcquisitionId);
            if (acquisition == null)
            {
                TempData["message"] = "Invalid acquisition.";
                TempData["messageType"] = "error";
                return RedirectToAction("Details", new { id = model.AcquisitionId });
            }

            var payment = model.NewPayment;
            payment.PaymentDate = DateTime.UtcNow;

            _context.Payments.Add(payment);
            await _context.SaveChangesAsync();

            _logService.LogAction(
                int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0"),
                "Acquisition Payment",
                $"{User.Identity?.Name ?? "Unknown"} recorded a payment of ₱{payment.Amount:N2} for Acquisition #{acquisition.Id}."
            );

            TempData["message"] = "Payment recorded successfully!";
            TempData["messageType"] = "success";
            return RedirectToAction("Details", new { id = acquisition.Id });
        }

        [HttpGet]
        public async Task<IActionResult> Recommendations()
        {
            // Load all books
            var books = await _context.Books
                .Include(b => b.BookCopy)
                .ToListAsync();

            // Load acquisitions with vendor
            var acquisitions = await _context.Acquisitions
                .Include(a => a.Vendor)
                .ToListAsync();

            // Build recommendation list
            var recs = books.Select(b =>
            {
                // Find latest acquisition by ISBN (best match)
                var acq = acquisitions
                    .Where(a => a.ISBN != null && a.ISBN == b.ISBN)
                    .OrderByDescending(a => a.CreatedAt)
                    .FirstOrDefault();

                // Get last borrow transaction
                var lastTx = _context.Transactions
                    .Where(t => t.BookCopy.BookID == b.Id)
                    .OrderByDescending(t => t.TransactionDate)
                    .Select(t => t.TransactionDate)
                    .FirstOrDefault();

                return new RecommendationViewModel
                {
                    BookId = b.Id,
                    Title = b.Title,
                    TotalCopies = b.BookCopy.Count,
                    AvailableCopies = b.BookCopy.Count(c => c.Status == "Available"),
                    LostCopies = b.BookCopy.Count(c => c.Status == "Lost" || c.Status == "Damaged"),
                    LastTransaction = lastTx == default ? null : lastTx,
                    AcquisitionDate = acq?.CreatedAt ?? b.CreatedAt,
                    VendorName = acq?.Vendor?.Name ?? "Unknown",
                    NeedsReacquire =
                        (b.BookCopy.Count(c => c.Status == "Available") <= 1) ||
                        (lastTx == default || (DateTime.UtcNow - lastTx).TotalDays > 180)
                };
            }).ToList();

            // Pass dropdown data for filters
            ViewBag.AcquisitionTitles = recs.Select(r => r.Title).Distinct().ToList();
            ViewBag.Vendors = recs.Select(r => r.VendorName).Distinct().ToList();

            // Always return something
            return View(recs);
        }

    }
}