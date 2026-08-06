namespace PetelAssistants.Api.Helpers
{
    public static class PositionTypeHelper
    {
        public const string Weekly = "weekly";
        public const string Monthly = "monthly";

        public static bool TryNormalize(string? value, out string? normalized, out string? error)
        {
            normalized = null;
            error = null;

            if (string.IsNullOrWhiteSpace(value))
                return true;

            var n = value.Trim().ToLowerInvariant();
            if (n is Weekly or Monthly)
            {
                normalized = n;
                return true;
            }

            error = "סוג משרה חייב להיות weekly או monthly";
            return false;
        }

        public static string ToHebrew(string? value) => value switch
        {
            Weekly => "שבועי",
            Monthly => "חודשי",
            _ => "—"
        };
    }
}