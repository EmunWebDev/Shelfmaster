using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using test.Data;
using test.Entity;

namespace test.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class StudentController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public StudentController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/student
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Student>>> GetStudents()
        {
            return await _context.Students.ToListAsync();
        }

        // GET: api/student/{id}
        [HttpGet("{id}")]
        public async Task<ActionResult<Student>> GetStudent(int id)
        {
            var student = await _context.Students.FindAsync(id);
            if (student == null)
                return NotFound(new { message = "Student not found" });

            return student;
        }

        // POST: api/student
        [HttpPost]
        public async Task<ActionResult<Student>> CreateStudent(Student student)
        {
            // ensure email is unique in Students table
            var existing = await _context.Students.FirstOrDefaultAsync(s => s.Email == student.Email);
            if (existing != null)
                return BadRequest(new { message = "Email already exists in Students" });

            student.CreatedAt = DateTime.UtcNow;
            student.UpdatedAt = DateTime.UtcNow;

            _context.Students.Add(student);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetStudent), new { id = student.Id }, student);
        }

        // PUT: api/student/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateStudent(int id, Student student)
        {
            if (id != student.Id)
                return BadRequest(new { message = "ID mismatch" });

            var existing = await _context.Students.FindAsync(id);
            if (existing == null)
                return NotFound(new { message = "Student not found" });

            // update fields
            existing.FirstName = student.FirstName;
            existing.LastName = student.LastName;
            existing.Email = student.Email;
            existing.ContactNumber = student.ContactNumber;
            existing.Department = student.Department;
            existing.Program = student.Program;
            existing.YearLevel = student.YearLevel;
            existing.Section = student.Section;
            existing.Gender = student.Gender;
            existing.Status = student.Status;
            existing.Address = student.Address;
            existing.DateOfBirth = student.DateOfBirth;
            existing.EnrollmentDate = student.EnrollmentDate;
            existing.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return NoContent();
        }

        // DELETE: api/student/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteStudent(int id)
        {
            var student = await _context.Students.FindAsync(id);
            if (student == null)
                return NotFound(new { message = "Student not found" });

            _context.Students.Remove(student);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}
