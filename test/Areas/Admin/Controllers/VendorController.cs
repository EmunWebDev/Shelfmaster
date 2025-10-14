using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using test.Data;
using test.Entity;
using X.PagedList;
using X.PagedList.Extensions;

namespace test.Areas.Admin.Controllers
{
    [Authorize(Roles = "Admin")]
    [Area("Admin")]
    public class VendorController : Controller
    {
        private readonly ApplicationDbContext _context;

        public VendorController(ApplicationDbContext context)
        {
            _context = context;
        }

        //INDEX: List vendors with search + pagination
        [HttpGet]
        public IActionResult Index(string searchQuery, int? page)
        {
            int pageSize = 10;
            int pageNumber = page ?? 1;

            // Start queryable
            var vendors = _context.Vendors.AsQueryable();

            // Optional search
            if (!string.IsNullOrEmpty(searchQuery))
            {
                vendors = vendors.Where(v =>
                    v.Name.Contains(searchQuery) ||
                    v.ContactPerson.Contains(searchQuery) ||
                    v.Email.Contains(searchQuery));
            }

            // Pagination
            var pagedList = vendors
                .OrderBy(v => v.Name) // Optional sorting
                .ToPagedList(pageNumber, pageSize);

            return View("Index", pagedList);
        }

        // ➕ SHOW CREATE FORM
        [HttpGet]
        public IActionResult New()
        {
            return View();
        }

        // ✅ CREATE NEW VENDOR
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateVendor(Vendor vendor)
        {
            if (!ModelState.IsValid)
            {
                return View("New", vendor);
            }

            // Check for duplicate name or email
            bool nameExists = await _context.Vendors.AnyAsync(v => v.Name == vendor.Name);
            if (nameExists)
            {
                ModelState.AddModelError("Name", "A vendor with this name already exists.");
                return View("New", vendor);
            }

            bool emailExists = await _context.Vendors.AnyAsync(v => v.Email == vendor.Email);
            if (emailExists)
            {
                ModelState.AddModelError("Email", "A vendor with this email already exists.");
                return View("New", vendor);
            }

            vendor.CreatedAt = DateTime.Now;
            vendor.UpdatedAt = DateTime.Now;

            _context.Vendors.Add(vendor);
            await _context.SaveChangesAsync();

            TempData["message"] = "Vendor added successfully!";
            TempData["messageType"] = "success";
            return RedirectToAction("Index");
        }

        // ✏️ EDIT FORM
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var vendor = await _context.Vendors.FindAsync(id);
            if (vendor == null)
            {
                return NotFound();
            }

            return View(vendor);
        }

        // ✅ UPDATE VENDOR
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateVendor(Vendor vendor)
        {
            if (!ModelState.IsValid)
            {
                return View("Edit", vendor);
            }

            var existing = await _context.Vendors.FindAsync(vendor.Id);
            if (existing == null)
            {
                return NotFound();
            }

            // Optional: Check for duplicate name/email (excluding current record)
            bool nameExists = await _context.Vendors.AnyAsync(v =>
                v.Name == vendor.Name && v.Id != vendor.Id);
            if (nameExists)
            {
                ModelState.AddModelError("Name", "A vendor with this name already exists.");
                return View("Edit", vendor);
            }

            bool emailExists = await _context.Vendors.AnyAsync(v =>
                v.Email == vendor.Email && v.Id != vendor.Id);
            if (emailExists)
            {
                ModelState.AddModelError("Email", "A vendor with this email already exists.");
                return View("Edit", vendor);
            }

            // Update fields
            existing.Name = vendor.Name;
            existing.Address = vendor.Address;
            existing.ContactPerson = vendor.ContactPerson;
            existing.ContactNumber = vendor.ContactNumber;
            existing.Email = vendor.Email;
            existing.UpdatedAt = DateTime.Now;

            _context.Vendors.Update(existing);
            await _context.SaveChangesAsync();

            TempData["message"] = "Vendor updated successfully!";
            TempData["messageType"] = "success";
            return RedirectToAction("Index");
        }

        // 🗑️ DELETE VENDOR
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteVendor(int id)
        {
            var vendor = await _context.Vendors.FindAsync(id);
            if (vendor == null)
            {
                TempData["message"] = "Vendor not found.";
                TempData["messageType"] = "error";
                return RedirectToAction("Index");
            }

            _context.Vendors.Remove(vendor);
            await _context.SaveChangesAsync();

            TempData["message"] = "Vendor deleted successfully!";
            TempData["messageType"] = "success";
            return RedirectToAction("Index");
        }
    }
}
