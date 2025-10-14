using System;
using test.Entity;
using test.Data;

namespace test.Services
{
    public class LogService
    {
        private readonly ApplicationDbContext _context;

        public LogService(ApplicationDbContext context)
        {
            _context = context;
        }

        public void LogAction(int userId, string action, string details)
        {
            var log = new Log
            {
                UserID = userId,
                Action = action,
                Details = details,
                CreatedAt = DateTime.Now
            };

            _context.Logs.Add(log);
            _context.SaveChanges();
        }
    }
}
