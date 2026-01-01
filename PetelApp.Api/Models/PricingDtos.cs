
namespace PetelApp.Api.Models
{

// Request DTOs
    public class AddPricingElementRequest
    {
        public int YearId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int? SortOrder { get; set; }
        public string? CalculationLevel { get; set; }
        public string? AttributeToCheck { get; set; }
    }

    public class AddPricingCategoryRequest
    {
        public int PricingElement { get; set; }
        public int Category { get; set; }
        public bool? IsLowestLevel { get; set; }
        public decimal? Price { get; set; }
    }

    public class UpdatePricingCategoryRequest
    {
        public bool? IsLowestLevel { get; set; }
        public decimal? Price { get; set; }
    }

    public class AddPricingStepRequest
    {
        public int PricingElement { get; set; }
        public int Category { get; set; }
        public string ObjectCheck { get; set; } = string.Empty;
        public string ObjectElementCheck { get; set; } = string.Empty;
        public string ObjectElementValue { get; set; } = string.Empty;
        public decimal? Price { get; set; }
    }

    public class UpdatePricingStepRequest
    {
        public string ObjectCheck { get; set; } = string.Empty;
        public string ObjectElementCheck { get; set; } = string.Empty;
        public string ObjectElementValue { get; set; } = string.Empty;
        public decimal? Price { get; set; }
    }

    // Tracks DTOs
    public class AddTrackRequest
    {
        public int YearId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? ExternalCode { get; set; }
        public string[]? AvailableForClasses { get; set; }
    }

    public class UpdateTrackRequest
    {
        public string Name { get; set; } = string.Empty;
        public string? ExternalCode { get; set; }
        public string[]? AvailableForClasses { get; set; }
    }

    public class AddTrackLevelRequest
    {
        public int SchoolTrackId { get; set; }
        public string? Level { get; set; }
        public int MinHours { get; set; }
        public int? MaxHours { get; set; }
        public string[]? AvailableForClasses { get; set; }
    }

    public class UpdateTrackLevelRequest
    {
        public string? Level { get; set; }
        public int MinHours { get; set; }
        public int? MaxHours { get; set; }
        public string[]? AvailableForClasses { get; set; }
    }

    public class AddTrackPricingRequest
    {
        public int SchoolTrackId { get; set; }
        public decimal? Price { get; set; }
        public int? Category { get; set; }
        public int? LevelId { get; set; }
    }

    public class UpdateTrackPricingRequest
    {
        public decimal? Price { get; set; }
        public int? Category { get; set; }
    }

    // ==================== Export/Import Data Models ====================

    public class SchoolYearConfigExport
    {
        public DateTime ExportDate { get; set; }
        public int YearId { get; set; }
        public string YearName { get; set; } = string.Empty;
        public List<PricingElementExport> PricingElements { get; set; } = new();
        public List<DocumentTypeExport> DocumentTypes { get; set; } = new();
        public List<StudyProgramExport> StudyPrograms { get; set; } = new();
        public List<TrackExport> Tracks { get; set; } = new();
    }

    public class PricingElementExport
    {
        public string Name { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? CalculationLevel { get; set; }
        public string? AttributeToCheck { get; set; }
        public List<PricingCategoryExport> Categories { get; set; } = new();
    }

    public class PricingCategoryExport
    {
        public int Category { get; set; }
        public bool IsLowestLevel { get; set; }
        public decimal? Price { get; set; }
        public List<PricingStepExport> Steps { get; set; } = new();
    }

    public class PricingStepExport
    {
        public string ObjectCheck { get; set; } = string.Empty;
        public string ObjectElementCheck { get; set; } = string.Empty;
        public string ObjectElementValue { get; set; } = string.Empty;
        public decimal? Price { get; set; }
    }

    public class DocumentTypeExport
    {
        public string TypeName { get; set; } = string.Empty;
    }

    public class StudyProgramExport
    {
        public int Students { get; set; }
        public decimal? Price { get; set; }
    }

    public class TrackExport
    {
        public string TrackName { get; set; } = string.Empty;
        public string? Description { get; set; }        public string? ExternalCode { get; set; }
        public string[]? AvailableForClasses { get; set; }        public List<TrackLevelExport> Levels { get; set; } = new();
    }

    public class TrackLevelExport
    {
        public string LevelName { get; set; } = string.Empty;
        public string? Description { get; set; }        public int MinHours { get; set; }
        public int? MaxHours { get; set; }
        public string[]? AvailableForClasses { get; set; }        public List<TrackPricingExport> Pricing { get; set; } = new();
    }

    public class TrackPricingExport
    {
        public int Category { get; set; }
        public decimal? Price { get; set; }
    }

    public class ImportResult
    {
        public int PricingElements { get; set; }
        public int PricingCategories { get; set; }
        public int PricingSteps { get; set; }
        public int DocumentTypes { get; set; }
        public int StudyPrograms { get; set; }
        public int Tracks { get; set; }
        public int TrackLevels { get; set; }
        public int TrackPricing { get; set; }
    }

   /* public class AddTrackPricingRequest
    {
        public int SchoolTrackId { get; set; }
        public decimal? Price { get; set; }
        public int? Category { get; set; }
        public int? LevelId { get; set; }
    }

    public class UpdateTrackPricingRequest
    {
        public decimal? Price { get; set; }
        public int? Category { get; set; }
    }*/
}