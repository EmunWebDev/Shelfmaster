using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using test.Entity;

namespace test.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Author> Authors { get; set; }
        public DbSet<Publisher> Publishers { get; set; }
        public DbSet<Genre> Genres { get; set; }
        public DbSet<Book> Books { get; set; }
        public DbSet<BookCopy> BookCopies { get; set; }
        public DbSet<Transaction> Transactions { get; set; }
        public DbSet<Overdue> Overdues { get; set; }
        public DbSet<Penalty> Penalties { get; set; }
        public DbSet<Payment> Payments { get; set; }
        public DbSet<Log> Logs { get; set; }
        public DbSet<Report> Reports { get; set; }
        public DbSet<Student> Students { get; set; }
        public DbSet<Staff> Staffs { get; set; }
        public DbSet<Vendor> Vendors { get; set; }
        public DbSet<Acquisition> Acquisitions { get; set; }
        public DbSet<AcquisitionPayment> AcquisitionPayments { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Unique constraints
            modelBuilder.Entity<User>().HasIndex(u => u.Email).IsUnique();
            modelBuilder.Entity<Author>().HasIndex(a => a.Name).IsUnique();
            modelBuilder.Entity<Book>().HasIndex(b => b.ISBN).IsUnique();
            modelBuilder.Entity<BookCopy>().HasIndex(bc => bc.CopyNumber).IsUnique();
            modelBuilder.Entity<Genre>().HasIndex(g => g.Name).IsUnique();
            modelBuilder.Entity<Publisher>().HasIndex(p => p.Name).IsUnique();

            // Relationships
            modelBuilder.Entity<Author>()
                .HasMany(a => a.Book)
                .WithOne(b => b.Author)
                .HasForeignKey(b => b.AuthorID);

            modelBuilder.Entity<Publisher>()
                .HasMany(p => p.Book)
                .WithOne(b => b.Publisher)
                .HasForeignKey(b => b.PublisherID);

            modelBuilder.Entity<Genre>()
                .HasMany(g => g.Book)
                .WithOne(b => b.Genre)
                .HasForeignKey(b => b.GenreID);

            modelBuilder.Entity<Book>()
                .HasMany(b => b.BookCopy)
                .WithOne(bc => bc.Book)
                .HasForeignKey(bc => bc.BookID);

            modelBuilder.Entity<User>()
                .HasMany(u => u.Transaction)
                .WithOne(t => t.User)
                .HasForeignKey(t => t.BorrowerID);

            modelBuilder.Entity<BookCopy>()
                .HasMany(bc => bc.Transaction)
                .WithOne(t => t.BookCopy)
                .HasForeignKey(t => t.BookCopyID);

            modelBuilder.Entity<Transaction>()
                .HasMany(t => t.Overdue)
                .WithOne(o => o.Transaction)
                .HasForeignKey(o => o.TransactionID);

            modelBuilder.Entity<Transaction>()
                .HasMany(t => t.Penalty)
                .WithOne(pe => pe.Transaction)
                .HasForeignKey(pe => pe.TransactionID);

            modelBuilder.Entity<Transaction>()
              .HasOne(t => t.Payment)
              .WithOne(pa => pa.Transaction)
              .HasForeignKey<Payment>(pa => pa.TransactionID);

            modelBuilder.Entity<User>()
                .HasMany(t => t.Log)
                .WithOne(u => u.User)
                .HasForeignKey(u => u.UserID);

            // Vendor has many Acquisitions
            modelBuilder.Entity<Vendor>()
                .HasMany(v => v.Acquisitions)
                .WithOne(a => a.Vendor)
                .HasForeignKey(a => a.VendorId)
                .OnDelete(DeleteBehavior.Restrict);

            // Book has many Acquisitions
            modelBuilder.Entity<Book>()
                .HasMany(b => b.Acquisitions)
                .WithOne(a => a.Book)
                .HasForeignKey(a => a.BookId)
                .OnDelete(DeleteBehavior.Cascade);

            SeedData(modelBuilder);
        }

        private void SeedData(ModelBuilder modelBuilder)
        {
            var fixedDate = new DateTime(2025, 9, 9);

            // --- Admin User
            modelBuilder.Entity<User>().HasData(
                new User
                {
                    Id = 1,
                    Name = "Mark Labastilla",
                    Role = "Admin",
                    Username = "Mark",
                    Email = "m.labastilla.350210@umindanao.edu.ph",
                    IsVerified = true,
                    Password = "AQAAAAIAAYagAAAAEEGzwvxoit7JQAKAP2COI9QtWY/TX19871hcKTefuIJU1OobbLYxgdKubb2GHjTcRg==",
                    CreatedAt = new DateTime(2025, 1, 1),
                    UpdatedAt = new DateTime(2025, 1, 1),
                }
            );

            modelBuilder.Entity<Author>().HasData(
                    new Author { Id = 1, Name = "David Allen", CreatedAt = fixedDate, UpdatedAt = fixedDate },
                    new Author { Id = 2, Name = "Raymond A. Serway", CreatedAt = fixedDate, UpdatedAt = fixedDate }
                );

            modelBuilder.Entity<Publisher>().HasData(
                    new Publisher { Id = 1, Name = "Penguin Publishing Group", Address = "London, UK", ContactNum = "09123456789", Email = "customerservice@cebookshop.com", CreatedAt = fixedDate, UpdatedAt = fixedDate },
                    new Publisher { Id = 2, Name = "Brooks/Cole", Address = "test", ContactNum = "09123456789", Email = "customerservice@cebookshop.com", CreatedAt = fixedDate, UpdatedAt = fixedDate }
                );

            modelBuilder.Entity<Genre>().HasData(
                    new Genre { Id = 1, Name = "Self-Help", CreatedAt = fixedDate, UpdatedAt = fixedDate },
                    new Genre { Id = 2, Name = "Calculus & mathematical analysis", CreatedAt = fixedDate, UpdatedAt = fixedDate }
                );

            modelBuilder.Entity<Vendor>().HasData(
                    new Vendor {
                        Id = 1,
                        Name = "C&E Adaptive Learning Solution",
                        Address = "839 EDSA, South Triangle, Quezon City 1103",
                        ContactPerson = "John Doe",
                        ContactNumber = "(632) 8929 5088",
                        Email = "customerservice@cebookshop.com",
                        CreatedAt = fixedDate
                    }
                );

            modelBuilder.Entity<Vendor>().HasData(
                    new Vendor
                    {
                        Id = 2,
                        Name = "Pandayan Bookshop Incorporated",
                        Address = "783 IRC Compound, Gen. Luis St., Paso de Blas, Valenzuela City",
                        ContactPerson = "Pandayan",
                        ContactNumber = "8990-0909",
                        Email = "pandayan.on@gmail.com",
                        CreatedAt = fixedDate
                    }
                );
            //// --- Authors (10)
            //modelBuilder.Entity<Author>().HasData(
            //    new Author { Id = 1, Name = "J.K. Rowling", CreatedAt = fixedDate, UpdatedAt = fixedDate },
            //    new Author { Id = 2, Name = "George R.R. Martin", CreatedAt = fixedDate, UpdatedAt = fixedDate },
            //    new Author { Id = 3, Name = "Agatha Christie", CreatedAt = fixedDate, UpdatedAt = fixedDate },
            //    new Author { Id = 4, Name = "Stephen King", CreatedAt = fixedDate, UpdatedAt = fixedDate },
            //    new Author { Id = 5, Name = "Haruki Murakami", CreatedAt = fixedDate, UpdatedAt = fixedDate },
            //    new Author { Id = 6, Name = "Jane Austen", CreatedAt = fixedDate, UpdatedAt = fixedDate },
            //    new Author { Id = 7, Name = "Mark Twain", CreatedAt = fixedDate, UpdatedAt = fixedDate },
            //    new Author { Id = 8, Name = "Leo Tolstoy", CreatedAt = fixedDate, UpdatedAt = fixedDate },
            //    new Author { Id = 9, Name = "Ernest Hemingway", CreatedAt = fixedDate, UpdatedAt = fixedDate },
            //    new Author { Id = 10, Name = "Isabel Allende", CreatedAt = fixedDate, UpdatedAt = fixedDate }
            //);

            //// --- Publishers (10)
            //modelBuilder.Entity<Publisher>().HasData(
            //    new Publisher { Id = 1, Name = "Penguin Books", Address = "London, UK", ContactNum = "123456789", Email = "penguin@books.com", CreatedAt = fixedDate, UpdatedAt = fixedDate },
            //    new Publisher { Id = 2, Name = "HarperCollins", Address = "New York, USA", ContactNum = "987654321", Email = "harper@books.com", CreatedAt = fixedDate, UpdatedAt = fixedDate },
            //    new Publisher { Id = 3, Name = "Macmillan", Address = "Berlin, Germany", ContactNum = "1122334455", Email = "macmillan@books.com", CreatedAt = fixedDate, UpdatedAt = fixedDate },
            //    new Publisher { Id = 4, Name = "Random House", Address = "Paris, France", ContactNum = "9988776655", Email = "random@house.com", CreatedAt = fixedDate, UpdatedAt = fixedDate },
            //    new Publisher { Id = 5, Name = "Simon & Schuster", Address = "Toronto, Canada", ContactNum = "5566778899", Email = "simon@schuster.com", CreatedAt = fixedDate, UpdatedAt = fixedDate },
            //    new Publisher { Id = 6, Name = "Oxford Press", Address = "Oxford, UK", ContactNum = "6677889900", Email = "oxford@press.com", CreatedAt = fixedDate, UpdatedAt = fixedDate },
            //    new Publisher { Id = 7, Name = "Cambridge Press", Address = "Cambridge, UK", ContactNum = "4455667788", Email = "cambridge@press.com", CreatedAt = fixedDate, UpdatedAt = fixedDate },
            //    new Publisher { Id = 8, Name = "Scholastic", Address = "Sydney, Australia", ContactNum = "3344556677", Email = "scholastic@books.com", CreatedAt = fixedDate, UpdatedAt = fixedDate },
            //    new Publisher { Id = 9, Name = "Hachette Livre", Address = "Paris, France", ContactNum = "2233445566", Email = "hachette@books.com", CreatedAt = fixedDate, UpdatedAt = fixedDate },
            //    new Publisher { Id = 10, Name = "Springer", Address = "Zurich, Switzerland", ContactNum = "1122446688", Email = "springer@books.com", CreatedAt = fixedDate, UpdatedAt = fixedDate }
            //);

            //// --- Genres (15)
            //modelBuilder.Entity<Genre>().HasData(
            //    new Genre { Id = 1, Name = "Romance", CreatedAt = fixedDate, UpdatedAt = fixedDate },
            //    new Genre { Id = 2, Name = "Fantasy", CreatedAt = fixedDate, UpdatedAt = fixedDate },
            //    new Genre { Id = 3, Name = "Science Fiction", CreatedAt = fixedDate, UpdatedAt = fixedDate },
            //    new Genre { Id = 4, Name = "Mystery", CreatedAt = fixedDate, UpdatedAt = fixedDate },
            //    new Genre { Id = 5, Name = "Thriller", CreatedAt = fixedDate, UpdatedAt = fixedDate },
            //    new Genre { Id = 6, Name = "Horror", CreatedAt = fixedDate, UpdatedAt = fixedDate },
            //    new Genre { Id = 7, Name = "Historical", CreatedAt = fixedDate, UpdatedAt = fixedDate },
            //    new Genre { Id = 8, Name = "Non-fiction", CreatedAt = fixedDate, UpdatedAt = fixedDate },
            //    new Genre { Id = 9, Name = "Biography", CreatedAt = fixedDate, UpdatedAt = fixedDate },
            //    new Genre { Id = 10, Name = "Self-Help", CreatedAt = fixedDate, UpdatedAt = fixedDate },
            //    new Genre { Id = 11, Name = "Poetry", CreatedAt = fixedDate, UpdatedAt = fixedDate },
            //    new Genre { Id = 12, Name = "Drama", CreatedAt = fixedDate, UpdatedAt = fixedDate },
            //    new Genre { Id = 13, Name = "Adventure", CreatedAt = fixedDate, UpdatedAt = fixedDate },
            //    new Genre { Id = 14, Name = "Graphic Novel", CreatedAt = fixedDate, UpdatedAt = fixedDate },
            //    new Genre { Id = 15, Name = "Philosophy", CreatedAt = fixedDate, UpdatedAt = fixedDate }
            //);

            // --- Students (Sample)
            modelBuilder.Entity<Student>().HasData(
                new Student
                {
                    Id = 1,
                    StudentNumber = "537937",
                    FirstName = "Emmanuel",
                    LastName = "Felipe",
                    Email = "e.felipe.537937@umindanao.edu.ph",
                    ContactNumber = "09664071773",
                    Department = "College of Computing Education",
                    Program = "BS Information Technology",
                    YearLevel = "3rd Year",
                    Section = "A",
                    Gender = Enums.Gender.Male,
                    Status = Enums.StudentStatus.Enrolled,
                    Address = "Davao City",
                    DateOfBirth = new DateTime(2002, 6, 13),
                    EnrollmentDate = new DateTime(2025, 2, 2),
                    CreatedAt = new DateTime(2025, 2, 2),
                    UpdatedAt = new DateTime(2025, 2, 2)
                }
            );

            modelBuilder.Entity<Student>().HasData(
                new Student
                {
                    Id = 2,
                    StudentNumber = "535410",
                    FirstName = "Allen Wrinse",
                    LastName = "Malagamba",
                    Email = "a.malagamba.535410@umindanao.edu.ph",
                    ContactNumber = "09664071773",
                    Department = "College of Computing Education",
                    Program = "BS Information Technology",
                    YearLevel = "4th Year",
                    Section = "A",
                    Gender = Enums.Gender.Male,
                    Status = Enums.StudentStatus.Enrolled,
                    Address = "Panabo City",
                    DateOfBirth = new DateTime(2003, 1, 1),
                    EnrollmentDate = new DateTime(2025, 2, 2),
                    CreatedAt = new DateTime(2025, 2, 2),
                    UpdatedAt = new DateTime(2025, 2, 2)
                }
            );

            modelBuilder.Entity<Student>().HasData(
                new Student
                {
                    Id = 3,
                    StudentNumber = "536003",
                    FirstName = "Brian",
                    LastName = "Entero",
                    Email = "b.entero.536003@umindanao.edu.ph",
                    ContactNumber = "09665000831",
                    Department = "College of Computing Education",
                    Program = "BS Information Technology",
                    YearLevel = "3rd Year",
                    Section = "A",
                    Gender = Enums.Gender.Male,
                    Status = Enums.StudentStatus.Enrolled,
                    Address = "Davao City",
                    DateOfBirth = new DateTime(2002, 6, 13),
                    EnrollmentDate = new DateTime(2025, 2, 2),
                    CreatedAt = new DateTime(2025, 2, 2),
                    UpdatedAt = new DateTime(2025, 2, 2)
                }
            );

            modelBuilder.Entity<Student>().HasData(
                new Student
                {
                    Id = 4,
                    StudentNumber = "482253",
                    FirstName = "Anthony Gerald",
                    LastName = "Gandeza",
                    Email = "a.gandeza.482253@umindanao.edu.ph",
                    ContactNumber = "09987654321",
                    Department = "College of Computing Education",
                    Program = "BS Information Technology",
                    YearLevel = "4th Year",
                    Section = "A",
                    Gender = Enums.Gender.Male,
                    Status = Enums.StudentStatus.Enrolled,
                    Address = "Davao City",
                    DateOfBirth = new DateTime(2002, 6, 13),
                    EnrollmentDate = new DateTime(2025, 2, 2),
                    CreatedAt = new DateTime(2025, 2, 2),
                    UpdatedAt = new DateTime(2025, 2, 2)
                }
            );

            // --- Staff (Sample) 
            modelBuilder.Entity<Staff>().HasData(
                new Staff
                {
                    Id = 1,
                    StaffNumber = "543231",
                    FirstName = "Miracle",
                    LastName = "Sample",
                    Email = "miracle0998@gmail.com",
                    ContactNumber = "09987654321",
                    Department = "Library Services",
                    Position = "Librarian",
                    Gender = Enums.Gender.Female,
                    Address = "123 Sample Street",
                    DateOfBirth = new DateTime(1998, 9, 9),
                    HireDate = new DateTime(2025, 2, 2),
                    IsActive = true,
                    CreatedAt = new DateTime(2025, 2, 2),
                    UpdatedAt = new DateTime(2025, 2, 2)
                }
            );
        }
    }
}
