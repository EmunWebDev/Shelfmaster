using test.Data;
using test.Entity;
using test.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using X.PagedList.Extensions;
using test.Areas.Admin.Controllers;

namespace test.Areas.Admin.Controllers
{
    [Authorize]
    [Area("Admin")]
    public class ArchiveController : BaseController
    {
        private readonly ApplicationDbContext _context;
        private readonly LogService _logService;

        public ArchiveController(ApplicationDbContext context, LogService logService)
        {
            _context = context;
            _logService = logService;
        }

        //Catalogue-level (Book grouped archive view)
        [HttpGet]
        public IActionResult Index(int? page)
        {
            int pageSize = 10;
            int pageNumber = page ?? 1;

            var archivedBooks = _context.Books
                .Include(b => b.Author)
                .Include(b => b.Genre)
                .Include(b => b.Publisher)
                .Where(b => _context.BookCopies.Any(bc => bc.BookID == b.Id && bc.Status == "Archived" && !bc.IsDeleted))
                .ToPagedList(pageNumber, pageSize);

            return View("Index", archivedBooks);
        }

        // 📌 Copy-level archive view
        [HttpGet]
        public IActionResult CopiesIndex(int? page, string search)
        {
            int pageSize = 10;
            int pageNumber = page ?? 1;

            var query = _context.BookCopies
                .Include(bc => bc.Book).ThenInclude(b => b.Author)
                .Include(bc => bc.Book.Genre)
                .Include(bc => bc.Book.Publisher)
                .Where(bc => bc.Status == "Archived" && !bc.IsDeleted);

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(c => c.CopyNumber.Contains(search));
            }

            var archivedCopies = query
                .OrderByDescending(bc => bc.ArchivedAt)
                .ToPagedList(pageNumber, pageSize);

            return View("CopiesIndex", archivedCopies);
        }

        // 📌 Soft delete archive (empty archive)
        [HttpPost]
        public IActionResult EmptyArchive()
        {
            try
            {
                var archivedCopies = _context.BookCopies
                    .Where(bc => bc.Status == "Archived" && !bc.IsDeleted)
                    .ToList();

                if (!archivedCopies.Any())
                {
                    TempData["message"] = "There is nothing in archive to delete.";
                    TempData["messageType"] = "error";
                    return RedirectToAction("Index");
                }

                foreach (var copy in archivedCopies)
                {
                    copy.IsDeleted = true;
                    copy.UpdatedAt = DateTime.Now;
                }
                _context.SaveChanges();

                _logService.LogAction(UserId, "Soft Delete Archive", $"{Username} soft deleted {archivedCopies.Count} archived copies.");

                TempData["message"] = $"{archivedCopies.Count} copies marked as deleted.";
                TempData["messageType"] = "success";
            }
            catch (Exception ex)
            {
                TempData["message"] = "An error occurred: " + ex.Message;
                TempData["messageType"] = "error";
            }

            return RedirectToAction("Index");
        }

        // 📌 Restore full book catalogue (all copies)
        [HttpPost]
        public IActionResult Restore(int id)
        {
            var book = _context.Books.Find(id);
            if (book == null) return NotFound();

            var bookCopies = _context.BookCopies
                .Where(bc => bc.BookID == id && !bc.IsDeleted)
                .ToList();

            foreach (var copy in bookCopies)
            {
                copy.Status = "Available";
                copy.ArchiveReason = null;
                copy.ArchivedAt = null;
                copy.UpdatedAt = DateTime.Now;
            }
            _context.SaveChanges();

            _logService.LogAction(UserId, "Restore Book", $"{Username} restored Book #{book.Id}.");

            TempData["message"] = "Book and its copies have been restored.";
            TempData["messageType"] = "success";
            return RedirectToAction("Index");
        }

        // 📌 Restore per copy
        [HttpPost]
        public IActionResult RestoreCopy(int id)
        {
            var copy = _context.BookCopies.FirstOrDefault(c => c.Id == id && !c.IsDeleted);
            if (copy == null) return NotFound();

            copy.Status = "Available";
            copy.ArchiveReason = null;
            copy.ArchivedAt = null;
            copy.UpdatedAt = DateTime.Now;

            _context.SaveChanges();

            _logService.LogAction(UserId, "Restore Copy", $"{Username} restored copy {copy.CopyNumber}.");

            TempData["message"] = $"Copy {copy.CopyNumber} restored successfully!";
            TempData["messageType"] = "success";
            return RedirectToAction("CopiesIndex");
        }

        // 📌 Archive a copy
        [HttpPost]
        public IActionResult ArchiveCopy(int id, string reason)
        {
            var copy = _context.BookCopies.FirstOrDefault(bc => bc.Id == id && !bc.IsDeleted);
            if (copy == null) return NotFound();

            copy.Status = "Archived";
            copy.ArchiveReason = reason;
            copy.ArchivedAt = DateTime.Now;
            copy.UpdatedAt = DateTime.Now;

            _context.SaveChanges();

            _logService.LogAction(UserId, "Archive Copy", $"{Username} archived copy {copy.CopyNumber} (Reason: {reason})");

            TempData["message"] = "Book copy archived successfully!";
            TempData["messageType"] = "success";
            return RedirectToAction("ManageCopies", "Book", new { id = copy.BookID });
        }

        // 📌 Soft delete selected copies
        [HttpPost]
        public IActionResult DeleteCopies(int[] ids)
        {
            var copies = _context.BookCopies.Where(c => ids.Contains(c.Id) && !c.IsDeleted).ToList();

            foreach (var copy in copies)
            {
                copy.IsDeleted = true;
                copy.UpdatedAt = DateTime.Now;
            }
            _context.SaveChanges();

            _logService.LogAction(UserId, "Soft Delete Copies", $"{Username} deleted {copies.Count} copies.");

            TempData["message"] = $"{copies.Count} copies marked as deleted.";
            TempData["messageType"] = "success";
            return RedirectToAction("CopiesIndex");
        }

        [HttpGet]
        public IActionResult DeletedCopies(int? page, string search)
        {
            int pageSize = 10;
            int pageNumber = page ?? 1;

            var query = _context.BookCopies
                .Include(bc => bc.Book).ThenInclude(b => b.Author)
                .Include(bc => bc.Book.Genre)
                .Include(bc => bc.Book.Publisher)
                .Where(bc => bc.IsDeleted);

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(c => c.CopyNumber.Contains(search));
            }

            var deletedCopies = query
                .OrderByDescending(bc => bc.UpdatedAt)
                .ToPagedList(pageNumber, pageSize);

            return View("DeletedCopies", deletedCopies);
        }

        [HttpPost]
        public IActionResult RestoreDeletedCopy(int id)
        {
            var copy = _context.BookCopies.FirstOrDefault(c => c.Id == id && c.IsDeleted);
            if (copy == null) return NotFound();

            copy.IsDeleted = false;
            copy.Status = "Available"; // restore as available
            copy.UpdatedAt = DateTime.Now;

            _context.SaveChanges();

            _logService.LogAction(UserId, "Restore Deleted Copy", $"{Username} restored soft-deleted copy {copy.CopyNumber}.");

            TempData["message"] = $"Deleted copy {copy.CopyNumber} restored successfully!";
            TempData["messageType"] = "success";
            return RedirectToAction("DeletedCopies");
        }

        [HttpPost]
        public IActionResult SoftDeleteSelected(List<int> selectedIds)
        {
            if (selectedIds == null || !selectedIds.Any())
            {
                TempData["message"] = "No copies selected.";
                TempData["messageType"] = "warning";
                return RedirectToAction("CopiesIndex");
            }

            var copies = _context.BookCopies.Where(c => selectedIds.Contains(c.Id)).ToList();

            foreach (var copy in copies)
            {
                copy.IsDeleted = true;
                copy.UpdatedAt = DateTime.Now;
            }

            _context.SaveChanges();

            _logService.LogAction(UserId, "Soft Delete Copies", $"{Username} soft deleted {copies.Count} copies.");

            TempData["message"] = $"{copies.Count} copies soft deleted successfully.";
            TempData["messageType"] = "success";

            return RedirectToAction("CopiesIndex");
        }

    }

}
