namespace test.Enums
{
    public enum UserStatus
    {
        Inactive = 0,
        Active = 1,
        Suspended = 2
    }

    public enum StudentStatus
    {
        Enrolled = 0,
        Graduated = 1,
        Dropout = 2,
        Transferred = 3,
        Suspended = 4
    }

    public enum StaffPosition
    {
        Librarian = 0,
        Cashier = 1
    }

    public enum Gender
    {
        Male = 0,
        Female = 1,
        Other = 2
    }

    public enum AcquisitionStatus
    {
        Requested,   // Librarian created
        Approved,    // Admin approved
        Delivered,   // Vendor delivered
        Checked,   // Librarian inspected
        Catalogued,  // Added to Books + Copies
        Rejected     // Admin rejected
    }
}
