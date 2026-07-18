namespace PetelAssistants.Api.Services
{
    /// <summary>
    /// Israeli national ID (תעודת זהות) checksum — Luhn-like algorithm
    /// identical to PetelATH StudentsFileProcessor / historical EntitlementService logic.
    /// </summary>
    public static class IsraeliIdHelper
    {
        public static string DigitsOnly(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;
            return new string(value.Where(char.IsDigit).ToArray());
        }

        /// <summary>
        /// Canonical 9-digit form for comparing IDs across sources that may drop a leading zero
        /// (e.g. salary import pads to 9; persons may store 8 digits).
        /// </summary>
        public static string ToCanonicalId(string? idNumber)
        {
            var digits = DigitsOnly(idNumber);
            if (string.IsNullOrEmpty(digits))
                return string.Empty;
            if (digits.Length > 9)
                digits = digits[^9..];
            return digits.PadLeft(9, '0');
        }

        public static bool IsValidIsraeliId(string idNumber)
        {
            if (idNumber.Length != 9 || !idNumber.All(char.IsDigit))
                return false;

            int sum = 0;
            for (int i = 0; i < 9; i++)
            {
                int digit = idNumber[i] - '0';
                int multiplied = digit * ((i % 2) + 1);
                if (multiplied > 9)
                    multiplied -= 9;
                sum += multiplied;
            }

            return sum % 10 == 0;
        }

        /// <summary>
        /// Computes the check digit for the first 8 digits (left-padded if shorter).
        /// </summary>
        public static char ComputeCheckDigit(string eightDigits)
        {
            if (eightDigits.Length != 8 || !eightDigits.All(char.IsDigit))
                throw new ArgumentException("Expected exactly 8 digits", nameof(eightDigits));

            int sum = 0;
            for (int i = 0; i < 8; i++)
            {
                int digit = eightDigits[i] - '0';
                int multiplied = digit * ((i % 2) + 1);
                if (multiplied > 9)
                    multiplied -= 9;
                sum += multiplied;
            }

            int check = (10 - (sum % 10)) % 10;
            return (char)('0' + check);
        }

        /// <summary>
        /// Normalizes a national ID for salary import.
        /// When <paramref name="includesCheckDigit"/> is true, verifies checksum (9 digits).
        /// When false, pads to 8 digits and appends the computed check digit.
        /// </summary>
        public static (string NormalizedId, bool HasWarning, string? WarningMessage) NormalizeForImport(
            string? rawId,
            bool includesCheckDigit)
        {
            var digits = DigitsOnly(rawId);
            if (string.IsNullOrEmpty(digits))
                return (string.Empty, false, null);

            if (includesCheckDigit)
            {
                if (digits.Length > 9)
                    digits = digits[^9..];
                else if (digits.Length < 9)
                    digits = digits.PadLeft(9, '0');

                if (!IsValidIsraeliId(digits))
                    return (digits, true, $"ספרת ביקורת שגויה בתעודת זהות: {digits}");

                return (digits, false, null);
            }

            // Without check digit: use up to 8 digits, pad left, append check digit
            if (digits.Length > 8)
                digits = digits[^8..];
            digits = digits.PadLeft(8, '0');
            var full = digits + ComputeCheckDigit(digits);
            return (full, false, null);
        }
    }
}
