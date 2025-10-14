using System.ComponentModel.DataAnnotations;

namespace test.Models
{
    public class TwoFactorSetupViewModel
    {
        public string? QrCodeUrl { get; set; }
        public string? ManualKey { get; set; }

        [Required(ErrorMessage = "Please enter the code from your authenticator app")]
        [Display(Name = "Verification Code")]
        public string? OtpCode { get; set; }
    }
}
