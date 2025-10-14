using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace test.Areas.Admin.Controllers
{
    public class BaseController : Controller
    {
        protected int UserId => int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        protected string Username => User.FindFirst(ClaimTypes.Name)?.Value ?? "Unknown";
        protected string UserRole => User.FindFirst(ClaimTypes.Role)?.Value ?? "Guest";
    }
}
