using System.Security.Cryptography;
using System.Text;

namespace ShiftTrackingApp.Helpers
{
    /// <summary>
    /// AES-256-CBC şifreleme / çözme yardımcısı.
    /// Yüz tanıma vektörlerini veritabanında şifreli saklamak için kullanılır.
    /// </summary>
    public static class EncryptionHelper
    {
        /// <summary>
        /// Verilen düz metni AES-256-CBC ile şifreler.
        /// IV (16 byte) şifreli verinin başına eklenir ve sonuç Base64 döner.
        /// </summary>
        public static string Encrypt(string plainText, string base64Key)
        {
            var key  = Convert.FromBase64String(base64Key);
            if (key.Length != 32)
                throw new ArgumentException("Şifreleme anahtarı tam olarak 32 byte (256 bit) olmalıdır.");

            using var aes = Aes.Create();
            aes.Key  = key;
            aes.Mode = CipherMode.CBC;
            aes.GenerateIV();

            using var encryptor  = aes.CreateEncryptor();
            var plainBytes  = Encoding.UTF8.GetBytes(plainText);
            var cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

            // IV + şifreli veri birleştir
            var result = new byte[aes.IV.Length + cipherBytes.Length];
            aes.IV.CopyTo(result, 0);
            cipherBytes.CopyTo(result, aes.IV.Length);
            return Convert.ToBase64String(result);
        }

        /// <summary>
        /// Encrypt() ile şifrelenmiş Base64 metni çözer.
        /// </summary>
        public static string Decrypt(string cipherText, string base64Key)
        {
            var key       = Convert.FromBase64String(base64Key);
            var fullBytes = Convert.FromBase64String(cipherText);

            using var aes = Aes.Create();
            aes.Key  = key;
            aes.Mode = CipherMode.CBC;

            // IV: ilk 16 byte
            var iv     = new byte[16];
            var cipher = new byte[fullBytes.Length - 16];
            Array.Copy(fullBytes, 0,  iv,     0, 16);
            Array.Copy(fullBytes, 16, cipher, 0, cipher.Length);
            aes.IV = iv;

            using var decryptor = aes.CreateDecryptor();
            var plainBytes = decryptor.TransformFinalBlock(cipher, 0, cipher.Length);
            return Encoding.UTF8.GetString(plainBytes);
        }
    }
}
