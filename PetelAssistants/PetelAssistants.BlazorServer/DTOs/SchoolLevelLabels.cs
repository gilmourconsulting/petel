namespace PetelAssistants.BlazorServer.DTOs
{
    /// <summary>
    /// Display labels for institution school_level codes (stored as English; shown as ministry שלב חינוך phrases).
    /// </summary>
    public static class SchoolLevelLabels
    {
        public const string Elementary = "elementary";
        public const string HighSchool = "high_school";
        public const string KindergartenType = "kindergarten";

        public const string ElementaryDisplay = "יסודי בלבד";
        public const string HighSchoolDisplay = "חט\"ב + עליונה";
        public const string KindergartenDisplay = "גן ילדים בלבד";

        public static string GetDisplayName(string? schoolLevel, string? orgUnitType = null)
        {
            if (string.Equals(orgUnitType, KindergartenType, StringComparison.OrdinalIgnoreCase))
                return KindergartenDisplay;

            if (string.Equals(schoolLevel, Elementary, StringComparison.OrdinalIgnoreCase))
                return ElementaryDisplay;
            if (string.Equals(schoolLevel, HighSchool, StringComparison.OrdinalIgnoreCase))
                return HighSchoolDisplay;

            return "—";
        }
    }
}
