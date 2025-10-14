using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using test.Data;
using System.Threading.Tasks;
using X.PagedList.Extensions;

namespace test.Areas.Admin.Controllers
{
    [Authorize(Roles = "Admin")]
    [Area("Admin")]
    public class LogController : Controller
    {
        private readonly ApplicationDbContext _context; 

        public LogController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(int? page)
        {
            int pageSize = 10;
            int pageNumber = page ?? 1;

            var logs = await _context.Logs
                .Include(l => l.User)
                .OrderByDescending(l => l.CreatedAt)
                .ToListAsync();

            var pagedList = logs.ToPagedList(pageNumber, pageSize);
            return View(pagedList);
        }
    }
}
