using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using System.Globalization;
using test.Data;
using test.Entity;
using test.Services;
using X.PagedList.Extensions;

namespace test.Areas.Admin.Controllers
{
    [Authorize(Roles = "Admin")]
    [Area("Admin")]
    public class ReportController : BaseController
    {
        private readonly ApplicationDbContext _context;
        private readonly LogService _logService;

        public ReportController(ApplicationDbContext context, LogService logService)
        {
            _context = context;
            _logService = logService;
        }

        [HttpGet]
        public async Task<IActionResult> ExportToPdf(int transactionId)
        {
            var transaction = await _context.Transactions
                .Include(t => t.User)
                .Include(t => t.Penalty)
                .Include(t => t.Payment)
                .Include(t => t.BookCopy)
                    .ThenInclude(bc => bc.Book)
                .FirstOrDefaultAsync(t => t.Id == transactionId);

            if (transaction == null)
            {
                return NotFound();
            }

            decimal totalPenalty = transaction.Penalty?.Sum(p => (decimal?)p.Amount) ?? 0;
            decimal totalPayment = transaction.Payment?.Amount ?? 0;
            string paymentStatus = totalPayment >= totalPenalty ? "Fully Paid" : "Partially Paid";

            // Borrower name enrichment (Student / Staff / fallback to User)
            var borrowerName =
                await _context.Students
                    .Where(s => s.Email == transaction.User.Email)
                    .Select(s => s.FirstName + " " + s.LastName)
                    .FirstOrDefaultAsync()
                ??
                await _context.Staffs
                    .Where(st => st.Email == transaction.User.Email)
                    .Select(st => st.FirstName + " " + st.LastName)
                    .FirstOrDefaultAsync()
                ?? transaction.User?.Name ?? "Unknown Borrower";

            // Generate PDF
            var pdfBytes = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(30);

                    page.Header()
                        .AlignCenter()
                        .Row(row =>
                        {
                            row.ConstantItem(50)
                                .AlignCenter()
                                .Image("wwwroot/images/logo/logo1.png")
                                .FitWidth();

                            row.RelativeItem()
                                .AlignCenter()
                                .Text("ShelfMaster - Transaction Report")
                                .FontSize(20)
                                .Bold()
                                .FontColor(Colors.Black);
                        });

                    page.Content().Column(col =>
                    {
                        col.Item().Text(" ");
                        col.Item().Text($"Transaction ID: {transaction.Id}").FontSize(14).Bold();
                        col.Item().Text(" ");
                        col.Item().Text($"Borrower: {borrowerName}").FontSize(14);   // ✅ fixed
                        col.Item().Text($"Book: {transaction.BookCopy?.Book?.Title}").FontSize(14);
                        col.Item().Text($"Copy No.: {transaction.BookCopy?.CopyNumber}").FontSize(14);
                        col.Item().Text($"Transaction Date: {transaction.TransactionDate:yyyy-MM-dd}").FontSize(14);
                        col.Item().Text($"Due Date: {transaction.DueDate:yyyy-MM-dd}").FontSize(14);
                        col.Item().Text($"Return Date: {transaction.ReturnDate?.ToString("yyyy-MM-dd") ?? "Not Returned"}").FontSize(14);
                        col.Item().Text($"Status: {transaction.Status}").FontSize(14).FontColor(Colors.Green.Darken2);

                        col.Item().PaddingVertical(10).LineHorizontal(1);

                        if (transaction.Penalty != null && transaction.Penalty.Any())
                        {
                            col.Item().Text("Penalty Details").FontSize(16).Bold().FontColor(Colors.Red.Darken2);

                            col.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.ConstantColumn(200);
                                    columns.ConstantColumn(100);
                                    columns.ConstantColumn(100);
                                });

                                table.Header(header =>
                                {
                                    header.Cell().Text("Reason").Bold();
                                    header.Cell().Text("Amount").Bold();
                                    header.Cell().Text("Status").Bold();
                                });

                                foreach (var penalty in transaction.Penalty)
                                {
                                    table.Cell().Text(penalty.Reason);
                                    table.Cell().Text($"PHP {penalty.Amount:N2}");
                                    table.Cell().Text(penalty.IsPaid ? "Paid" : "Unpaid")
                                        .FontColor(penalty.IsPaid ? Colors.Green.Darken2 : Colors.Red.Darken2);
                                }

                                table.Cell().Text("").Bold();
                                table.Cell().Text($"Total: PHP {totalPenalty:N2}").Bold();
                                table.Cell().Text(paymentStatus)
                                    .Bold()
                                    .FontColor(paymentStatus == "Fully Paid" ? Colors.Green.Darken2 : Colors.Orange.Darken2);
                            });
                        }
                        else
                        {
                            col.Item().Text("No penalties found.").FontSize(14).FontColor(Colors.Green.Darken1);
                        }
                    });

                    page.Footer()
                        .AlignCenter()
                        .Text("Generated by Shelfmaster Library System - " + DateTime.Now.ToString("yyyy-MM-dd HH:mm"))
                        .FontSize(10)
                        .Italic();
                });
            }).GeneratePdf();

            _logService.LogAction(
                UserId,
                "Generate Individual Report",
                $"{Username} generated a report of Transaction #{transactionId}."
            );

            return File(pdfBytes, "application/pdf", $"Transaction_{transactionId}.pdf");
        }


        [HttpGet]
        public async Task<IActionResult> GenerateReport(string type, int year, int? month, int? day)
        {
            try
            {
                IQueryable<Transaction> query = _context.Transactions
                    .Include(t => t.User)
                    .Include(t => t.Penalty)
                    .Include(t => t.Payment)
                    .Include(t => t.BookCopy)
                        .ThenInclude(bc => bc.Book);

                string fileNameSuffix = "";

                if (type == "daily" && month.HasValue && day.HasValue)
                {
                    query = query.Where(t => t.TransactionDate.Year == year &&
                                             t.TransactionDate.Month == month.Value &&
                                             t.TransactionDate.Day == day.Value);
                    fileNameSuffix = $"Daily_{year}_{month}_{day}";
                }
                else if (type == "weekly" && month.HasValue && day.HasValue)
                {
                    var startDate = new DateTime(year, month.Value, day.Value);
                    var endDate = startDate.AddDays(7);
                    query = query.Where(t => t.TransactionDate >= startDate && t.TransactionDate < endDate);
                    fileNameSuffix = $"Weekly_{year}_{month}_{day}";
                }
                else if (type == "monthly" && month.HasValue)
                {
                    query = query.Where(t => t.TransactionDate.Year == year &&
                                             t.TransactionDate.Month == month.Value);
                    fileNameSuffix = $"Monthly_{year}_{month}";
                }
                else
                {
                    TempData["message"] = "Invalid report filter.";
                    TempData["messageType"] = "error";
                    return RedirectToAction("Index");
                }

                var transactions = await query.ToListAsync();

                if (!transactions.Any())
                {
                    TempData["message"] = "No transactions found for the selected period.";
                    TempData["messageType"] = "info";
                    return RedirectToAction("Index");
                }

                //Using ClosedXML
                using var workbook = new XLWorkbook();
                var worksheet = workbook.Worksheets.Add($"Report {fileNameSuffix}");

                // Headers
                worksheet.Cell(1, 1).Value = "Transaction ID";
                worksheet.Cell(1, 2).Value = "Borrower";
                worksheet.Cell(1, 3).Value = "Copy No.";
                worksheet.Cell(1, 4).Value = "Book Title";
                worksheet.Cell(1, 5).Value = "Status";
                worksheet.Cell(1, 6).Value = "Transaction Date";
                worksheet.Cell(1, 7).Value = "Due Date";
                worksheet.Cell(1, 8).Value = "Return Date";
                worksheet.Cell(1, 9).Value = "Total Penalty";
                worksheet.Cell(1, 10).Value = "Total Payment";
                worksheet.Cell(1, 11).Value = "Payment Status";
                worksheet.Cell(1, 12).Value = "OR Number";

                int row = 2;
                foreach (var transaction in transactions)
                {
                    decimal totalPenalty = transaction.Penalty?.Sum(p => (decimal?)p.Amount) ?? 0;
                    decimal totalPayment = transaction.Payment?.Amount ?? 0;
                    string paymentStatus = transaction.Payment == null ? "" :
                        (totalPayment >= totalPenalty ? "Fully Paid" : "Partially Paid");

                    worksheet.Cell(row, 1).Value = transaction.Id;
                    worksheet.Cell(row, 2).Value = transaction.User?.Name;
                    worksheet.Cell(row, 3).Value = transaction.BookCopy?.CopyNumber;
                    worksheet.Cell(row, 4).Value = transaction.BookCopy?.Book?.Title;
                    worksheet.Cell(row, 5).Value = transaction.Status;
                    worksheet.Cell(row, 6).Value = transaction.TransactionDate.ToString("yyyy-MM-dd");
                    worksheet.Cell(row, 7).Value = transaction.DueDate.ToString("yyyy-MM-dd");
                    worksheet.Cell(row, 8).Value = transaction.ReturnDate?.ToString("yyyy-MM-dd") ?? "Not Returned";
                    worksheet.Cell(row, 9).Value = totalPenalty.ToString("C");
                    worksheet.Cell(row, 10).Value = totalPayment.ToString("C");
                    worksheet.Cell(row, 11).Value = paymentStatus;
                    worksheet.Cell(row, 12).Value = transaction.Payment?.ORNumber ?? "";

                    row++;
                }

                // Format headers
                var headerRange = worksheet.Range("A1:L1");
                headerRange.Style.Font.Bold = true;
                headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;

                worksheet.Columns().AdjustToContents();

                string reportsDirectory = Path.Combine("wwwroot/reports");
                if (!Directory.Exists(reportsDirectory))
                {
                    Directory.CreateDirectory(reportsDirectory);
                }

                string fileName = $"Report_{fileNameSuffix}.xlsx";
                string filePath = Path.Combine(reportsDirectory, fileName);
                string relativePath = $"/reports/{fileName}";

                workbook.SaveAs(filePath);

                var report = new Report
                {
                    Year = year,
                    Month = month,
                    Day = (type == "daily" || type == "weekly") ? day : null,
                    Type = type,
                    FilePath = relativePath,
                    GeneratedAt = DateTime.Now
                };

                _context.Reports.Add(report);
                await _context.SaveChangesAsync();

                _logService.LogAction(UserId, "Generate Report", $"{Username} generated a {type} report for {fileNameSuffix}");

                TempData["message"] = $"Successfully generated {type} report.";
                TempData["messageType"] = "success";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["message"] = "Error generating report: " + ex.Message;
                TempData["messageType"] = "error";
                return RedirectToAction("Index");
            }
        }

        public async Task<IActionResult> Index(string typeFilter, int? year, int? month, int? day, int? page)
        {
            int pageSize = 10;
            int pageNumber = page ?? 1;

            var query = _context.Reports.AsQueryable();

            if (!string.IsNullOrEmpty(typeFilter))
            {
                query = query.Where(r => r.Type == typeFilter);
            }

            if (year.HasValue)
            {
                query = query.Where(r => r.Year == year.Value);
            }

            if (month.HasValue)
            {
                query = query.Where(r => r.Month == month.Value);
            }

            if (day.HasValue)
            {
                query = query.Where(r => r.Day == day.Value);
            }

            var reports = await query
                .OrderByDescending(r => r.GeneratedAt)
                .ToListAsync();

            var pagedList = reports.ToPagedList(pageNumber, pageSize);

            return View(pagedList);
        }

    }
}
