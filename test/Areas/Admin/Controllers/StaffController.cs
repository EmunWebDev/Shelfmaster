using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using test.Data;
using test.Entity;

namespace test.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class StaffController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public StaffController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/staff
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Staff>>> GetStaffs()
        {
            return await _context.Staffs.ToListAsync();
        }

        // GET: api/staff/{id}
        [HttpGet("{id}")]
        public async Task<ActionResult<Staff>> GetStaff(int id)
        {
            var staff = await _context.Staffs.FindAsync(id);
            if (staff == null)
                return NotFound(new { message = "Staff not found" });

            return staff;
        }

        // POST: api/staff
        [HttpPost]
        public async Task<ActionResult<Staff>> CreateStaff(Staff staff)
        {
            // ensure email is unique in Staffs table
            var existing = await _context.Staffs.FirstOrDefaultAsync(s => s.Email == staff.Email);
            if (existing != null)
                return BadRequest(new { message = "Email already exists in Staffs" });

            staff.CreatedAt = DateTime.UtcNow;
            staff.UpdatedAt = DateTime.UtcNow;

            _context.Staffs.Add(staff);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetStaff), new { id = staff.Id }, staff);
        }

        // PUT: api/staff/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateStaff(int id, Staff staff)
        {
            if (id != staff.Id)
                return BadRequest(new { message = "ID mismatch" });

            var existing = await _context.Staffs.FindAsync(id);
            if (existing == null)
                return NotFound(new { message = "Staff not found" });

            existing.FirstName = staff.FirstName;
            existing.LastName = staff.LastName;
            existing.Email = staff.Email;
            existing.ContactNumber = staff.ContactNumber;
            existing.Department = staff.Department;
            existing.Position = staff.Position;
            existing.Gender = staff.Gender;
            existing.IsActive = staff.IsActive;
            existing.Address = staff.Address;
            existing.DateOfBirth = staff.DateOfBirth;
            existing.HireDate = staff.HireDate;
            existing.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return NoContent();
        }

        // DELETE: api/staff/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteStaff(int id)
        {
            var staff = await _context.Staffs.FindAsync(id);
            if (staff == null)
                return NotFound(new { message = "Staff not found" });

            _context.Staffs.Remove(staff);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}
