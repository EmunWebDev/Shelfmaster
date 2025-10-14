using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using test.Data;
using test.Entity;
using Microsoft.EntityFrameworkCore;

namespace test.Services
{
    public class OverdueCheckerService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<OverdueCheckerService> _logger;

        public OverdueCheckerService(IServiceScopeFactory scopeFactory, ILogger<OverdueCheckerService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("OverdueCheckerService is running...");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using (var scope = _scopeFactory.CreateScope())
                    {
                        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                        var currentDate = DateTime.Now;

                        var overdueTransactions = dbContext.Transactions
                            .Include(t => t.BookCopy)
                            .Where(t => t.DueDate.AddDays(1) <= currentDate && t.ReturnDate == null && t.Status != "Lost" && t.Status != "Damaged" && t.Status != "Completed") 
                            .ToList();

                        foreach (var transaction in overdueTransactions)
                        {
                            if (transaction.BookCopy == null)
                            {
                                _logger.LogWarning($"Transaction ID {transaction.Id} has no associated BookCopy.");
                                continue;
                            }

                            transaction.BookCopy.Status = "Overdue";
                            transaction.Status = "Overdue";

                            var existingOverdue = dbContext.Overdues
                                .FirstOrDefault(o => o.TransactionID == transaction.Id);
                            var overdueDays = Math.Max(1, (currentDate - transaction.DueDate).Days);

                            if (existingOverdue == null)
                            {
                               var overdueRecord = new Overdue
                                {
                                    TransactionID = transaction.Id,
                                    OverdueDays = overdueDays,
                                    CreatedAt = currentDate
                                };

                                dbContext.Overdues.Add(overdueRecord);
                            }
                            else
                            {
                                existingOverdue.OverdueDays = overdueDays;
                            }

                            var fineAmount = overdueDays * 25;
                            var existingPenalty = dbContext.Penalties
                                .FirstOrDefault(p => p.TransactionID == transaction.Id && p.Reason == "Overdue");

                            if (existingPenalty == null)
                            {
                                var penaltyRecord = new Penalty
                                {
                                    TransactionID = transaction.Id,
                                    Reason = "Overdue",
                                    Amount = fineAmount,
                                    IsPaid = false
                                };
                                dbContext.Penalties.Add(penaltyRecord);
                            }
                            else
                            {
                                existingPenalty.Amount = fineAmount;
                            }

                        }

                        dbContext.SaveChanges();

                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error in OverdueCheckerService: {ex.Message}");
                }

                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); 
            }
        }
    }
}