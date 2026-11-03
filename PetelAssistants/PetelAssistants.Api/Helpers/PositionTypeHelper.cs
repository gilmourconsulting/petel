namespace PetelAssistants.Api.Helpers
{
    public static class PositionTypeHelper
    {
        public const string Weekly = "weekly";
        public const string Monthly = "monthly";
        public const string Daily = "daily";

        public static bool TryNormalize(string? value, out string? normalized, out string? error)
        {
            normalized = null;
            error = null;

            if (string.IsNullOrWhiteSpace(value))
                return true;

            var n = value.Trim().ToLowerInvariant();
            if (n is Weekly or Monthly or Daily)
            {
                normalized = n;
                return true;
            }

            error = "סוג משרה חייב להיות weekly, monthly או daily";
            return false;
        }

        public static string ToHebrew(string? value) => value switch
        {
            Weekly => "שבועי",
            Monthly => "חודשי",
            Daily => "יומי",
            _ => "—"
        };
    }
}
