namespace PetelAssistants.Api.Services
{
    public static class MeitarDataFileNames
    {
        public const string Mutavim = "MUTAVIM";
        public const string Cheshbonit = "CHESHBONIT";
        public const string Sacal = "SACAL";
        public const string SacalCharigim = "SACALCHARIGIM";
        public const string Hasaot = "HASAOT";
        public const string Mucarim = "MUCARIM";
        public const string Azarolim = "AZAROLIM";
        public const string Gy003 = "GY003";
        public const string Gy019 = "GY019";
        public const string Gy033 = "GY033";
        public const string Hasmaslulim = "HASMASLULIM";
        public const string Hasnet = "HASNET";
        public const string Ichluskitot = "ICHLUSKITOT";
        public const string Misrot = "MISROT";
        public const string MisrotGy = "MISROTGY";
        public const string Moadon = "MOADON";
        public const string Shratim = "SHARATIM";
        public const string Shefi = "SHEFI";
        public const string Yadaniim = "YADANIIM";

        public static readonly IReadOnlyList<string> All = new[]
        {
            Mutavim,
            Cheshbonit,
            Sacal,
            SacalCharigim,
            Hasaot,
            Mucarim,
            Azarolim,
            Gy003,
            Gy019,
            Gy033,
            Hasmaslulim,
            Hasnet,
            Ichluskitot,
            Misrot,
            MisrotGy,
            Moadon,
            Shratim,
            Shefi,
            Yadaniim
        };

        private static readonly HashSet<string> Known = new(All, StringComparer.OrdinalIgnoreCase);

        public static bool IsSupported(string fileName) =>
            !string.IsNullOrWhiteSpace(fileName) && Known.Contains(fileName.Trim());
    }
}
