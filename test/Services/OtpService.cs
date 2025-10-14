using OtpNet;
using QRCoder;

namespace test.Services
{
    public class OtpService
    {
        public (string secretKey, string qrCodeUrl, string manualKey) GenerateTotpSetup(string email, string appName)
        {
            // Generate secret
            var secretKeyBytes = KeyGeneration.GenerateRandomKey(20);
            var base32Secret = Base32Encoding.ToString(secretKeyBytes);

            // Manually build the otpauth URI string (Google Authenticator format)
            var totpUri = $"otpauth://totp/{Uri.EscapeDataString(appName)}:{Uri.EscapeDataString(email)}" +
                          $"?secret={base32Secret}&issuer={Uri.EscapeDataString(appName)}&digits=6&period=30&algorithm=SHA1";

            // Create QR Code using QRCoder
            using var qrGenerator = new QRCodeGenerator();
            var qrCodeData = qrGenerator.CreateQrCode(totpUri, QRCodeGenerator.ECCLevel.Q);
            var qrCode = new PngByteQRCode(qrCodeData);
            var qrCodeBase64 = Convert.ToBase64String(qrCode.GetGraphic(10));

            var qrCodeUrl = $"data:image/png;base64,{qrCodeBase64}";

            return (base32Secret, qrCodeUrl, base32Secret);
        }

        public bool ValidateOtp(string secretKey, string userInputCode)
        {
            var totp = new Totp(Base32Encoding.ToBytes(secretKey));
            return totp.VerifyTotp(userInputCode, out long _, VerificationWindow.RfcSpecifiedNetworkDelay);
        }
    }
}
