namespace PetelAssistants.Api.Services
{
    public static class EntitlementInvalidReasons
    {
        public const string InvalidPupilId = "invalid_pupil_id";
        public const string InvalidSupportCode = "invalid_support_code";
        public const string MissingInstitution = "missing_institution";

        public static string? Join(IEnumerable<string> reasons)
        {
            var list = reasons
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .Distinct(StringComparer.Ordinal)
                .ToList();
            return list.Count == 0 ? null : string.Join(",", list);
        }

        public static List<string> Split(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return [];

            return raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }

        public static string ToHebrew(string code) => code switch
        {
            InvalidPupilId => "תעודת זהות לא תקינה",
            InvalidSupportCode => "קוד תומכת חינוך אינו 1",
            MissingInstitution => "מוסד לא נמצא במערכת",
            _ => code
        };

        public static string ToHebrewList(string? raw)
            => string.Join(", ", Split(raw).Select(ToHebrew));
    }

    public class EntitlementUploadValidity
    {
        public List<string> Reasons { get; set; } = [];
        public string? SourceInstitutionSymbol { get; set; }
        public string? SourceSupportCode { get; set; }

        public bool IsValid => Reasons.Count == 0;

        public string? ReasonsCsv => EntitlementInvalidReasons.Join(Reasons);
    }
}
