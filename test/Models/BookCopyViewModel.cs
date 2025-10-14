namespace test.Models
{
    public class BookCopyViewModel
    {
        public int Id { get; set; }
        public string CopyNumber { get; set; }
        public string Status { get; set; }
        public BookViewModel Book { get; set; } // 
    }
}
