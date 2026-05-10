using System.ComponentModel.DataAnnotations;

namespace ShiftTrackingApp.Helpers
{
    /// <summary>
    /// Kurumsal şifre politikası:
    /// - En az 8 karakter
    /// - En az bir büyük harf
    /// - En az bir küçük harf
    /// - En az bir rakam
    /// </summary>
    public class StrongPasswordAttribute : ValidationAttribute
    {
        public int MinLength       { get; set; } = 8;
        public bool RequireUpper   { get; set; } = true;
        public bool RequireLower   { get; set; } = true;
        public bool RequireDigit   { get; set; } = true;
        public bool RequireSymbol  { get; set; } = false;

        protected override ValidationResult? IsValid(object? value, ValidationContext ctx)
        {
            if (value is not string s || string.IsNullOrWhiteSpace(s))
                return new ValidationResult("Şifre zorunludur.");

            if (s.Length < MinLength)
                return new ValidationResult($"Şifre en az {MinLength} karakter olmalıdır.");

            if (RequireUpper && !s.Any(char.IsUpper))
                return new ValidationResult("Şifre en az bir büyük harf içermelidir.");

            if (RequireLower && !s.Any(char.IsLower))
                return new ValidationResult("Şifre en az bir küçük harf içermelidir.");

            if (RequireDigit && !s.Any(char.IsDigit))
                return new ValidationResult("Şifre en az bir rakam içermelidir.");

            if (RequireSymbol && !s.Any(c => !char.IsLetterOrDigit(c)))
                return new ValidationResult("Şifre en az bir özel karakter içermelidir.");

            return ValidationResult.Success;
        }
    }
}
