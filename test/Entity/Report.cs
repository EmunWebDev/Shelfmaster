
using System.ComponentModel.DataAnnotations;

namespace test.Entity
{
    public class Report
    {
        [Key]
        public int? Id { get; set; }
        public int? Year { get; set; }
        public int? Month { get; set; }
        public int? Day { get; set; }
        public string Type { get; set; }
        public string? FilePath { get; set; }

        public DateTime GeneratedAt { get; set; }
    }
}
