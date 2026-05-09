using ShiftTrackingApp.Helpers;
using Xunit;

namespace ShiftTrackingApp.Tests
{
    public class EncryptionHelperTests
    {
        // "ShiftexFaceEncryptionKey2026!Dev" = 32 bytes
        private const string ValidKey = "U2hpZnRleEZhY2VFbmNyeXB0aW9uS2V5MjAyNiFEZXY=";

        [Fact]
        public void Encrypt_ThenDecrypt_ReturnsOriginal()
        {
            const string plain = "Merhaba, bu bir test metnidir!";
            var cipher  = EncryptionHelper.Encrypt(plain, ValidKey);
            var decoded = EncryptionHelper.Decrypt(cipher, ValidKey);
            Assert.Equal(plain, decoded);
        }

        [Fact]
        public void Encrypt_SamePlaintext_DifferentCiphertext()
        {
            const string plain = "Aynı metin";
            var c1 = EncryptionHelper.Encrypt(plain, ValidKey);
            var c2 = EncryptionHelper.Encrypt(plain, ValidKey);
            // IV rastgele üretildiği için her şifreleme farklı sonuç verir
            Assert.NotEqual(c1, c2);
            // Ama ikisi de aynı metne çözülmeli
            Assert.Equal(plain, EncryptionHelper.Decrypt(c1, ValidKey));
            Assert.Equal(plain, EncryptionHelper.Decrypt(c2, ValidKey));
        }

        [Fact]
        public void Encrypt_InvalidKeyLength_ThrowsException()
        {
            // 16-byte key (too short for AES-256)
            var shortKey = Convert.ToBase64String(new byte[16]);
            Assert.Throws<ArgumentException>(() =>
                EncryptionHelper.Encrypt("test", shortKey));
        }

        [Fact]
        public void Decrypt_TamperedCiphertext_ThrowsException()
        {
            var cipher  = EncryptionHelper.Encrypt("orijinal", ValidKey);
            var tampered = cipher[..^4] + "XXXX";
            Assert.ThrowsAny<Exception>(() =>
                EncryptionHelper.Decrypt(tampered, ValidKey));
        }

        [Fact]
        public void Encrypt_JsonFloatArray_RoundTrip()
        {
            var descriptor  = Enumerable.Range(0, 128)
                .Select(i => (float)(i * 0.01)).ToArray();
            var json        = System.Text.Json.JsonSerializer.Serialize(descriptor);
            var cipher      = EncryptionHelper.Encrypt(json, ValidKey);
            var decrypted   = EncryptionHelper.Decrypt(cipher, ValidKey);
            var result      = System.Text.Json.JsonSerializer.Deserialize<float[]>(decrypted)!;
            Assert.Equal(128, result.Length);
            Assert.Equal(descriptor[0], result[0], precision: 5);
            Assert.Equal(descriptor[127], result[127], precision: 5);
        }
    }
}
