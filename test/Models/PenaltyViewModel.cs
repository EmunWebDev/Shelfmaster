namespace test.Models
{
    public class PenaltyViewModel
    {
        public int? Id { get; set; }
        public string? Reason { get; set; }
        public decimal? Amount { get; set; }

        public bool? IsPaid { get; set; }
    }

}
