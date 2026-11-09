using Microsoft.EntityFrameworkCore;
using PetelAssistants.Api.Data;
using PetelAssistants.Api.DTOs;
using PetelAssistants.Api.Models;

namespace PetelAssistants.Api.Services
{
    public class OrgUnitService
    {
        private static readonly HashSet<string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            InstitutionTypes.School,
            InstitutionTypes.Kindergarten
        };

        private static readonly HashSet<string> AllowedSchoolLevels = new(StringComparer.OrdinalIgnoreCase)
        {
            SchoolLevels.Elementary,
            SchoolLevels.HighSchool
        };

        private readonly AssistDbContext _context;
        private readonly SharedDbContext _sharedContext;
        private readonly ILogger<OrgUnitService> _logger;

        public OrgUnitService(
            AssistDbContext context,
            SharedDbContext sharedContext,
            ILogger<OrgUnitService> logger)
        {
            _context = context;
            _sharedContext = sharedContext;
            _logger = logger;
        }

        public async Task EnsureYearExistsAsync(int yearId)
        {
            var exists = await _sharedContext.HebrewYears.AsNoTracking()
                .AnyAsync(y => y.Id == yearId);
            if (!exists)
                throw new InvalidOperationException("שנה לא נמצאה");
        }

        public async Task<bool> HasRosterAsync(int yearId)
        {
            return await _context.InstitutionYearSettings.AsNoTracking()
                .AnyAsync(s => s.HebrewYearId == yearId);
        }

        public async Task<List<OrgUnitDto>> ListOrgUnitsAsync(int entityId, int yearId, string? typeFilter)
        {
            _ = entityId;
            await EnsureYearExistsAsync(yearId);

            var query = _context.InstitutionYearSettings.AsNoTracking()
                .Where(s => s.HebrewYearId == yearId);

            if (!string.IsNullOrWhiteSpace(typeFilter))
            {
                var type = typeFilter.Trim().ToLowerInvariant();
                if (!AllowedTypes.Contains(type))
                    throw new InvalidOperationException("סוג מוסד לא תקין");

                query = query.Where(s => s.InstitutionType == type);
            }

            var rows = await query
                .OrderBy(s => s.Name)
                .ToListAsync();

            return rows.Select(MapDto).ToList();
        }

        public async Task<int> CreateOrgUnitAsync(int entityId, int? userId, int yearId, CreateOrgUnitRequest request)
        {
            await EnsureYearExistsAsync(yearId);

            if (string.IsNullOrWhiteSpace(request.Name))
                throw new InvalidOperationException("שם מוסד הוא שדה חובה");

            var typeName = NormalizeType(request.OrgUnitType);
            var schoolLevel = NormalizeSchoolLevel(typeName, request.SchoolLevel);
            var name = request.Name.Trim();
            var symbol = NormalizeSymbol(request.Symbol);

            await EnsureYearNameUniqueAsync(yearId, name, excludeInstitutionId: null);
            if (symbol != null)
                await EnsureYearSymbolUniqueAsync(yearId, symbol, excludeInstitutionId: null);

            var now = DateTime.UtcNow;
            Institution? institution = null;

            if (symbol != null)
            {
                institution = await _context.Institutions
                    .FirstOrDefaultAsync(i => i.Symbol == symbol);
            }

            if (institution == null)
            {
                // Master name uniqueness: reuse existing master with same name when no symbol match.
                institution = await _context.Institutions
                    .FirstOrDefaultAsync(i => i.Name == name);

                if (institution == null)
                {
                    institution = new Institution
                    {
                        EntityId = entityId,
                        Name = name,
                        Symbol = symbol,
                        InstitutionType = typeName,
                        SchoolLevel = schoolLevel,
                        IsSpecialEducation = request.IsSpecialEducation,
                        IsActive = true,
                        UserId = userId,
                        UpdateUser = userId,
                        CreatedAt = now,
                        UpdatedAt = now
                    };
                    _context.Institutions.Add(institution);
                    await _context.SaveChangesAsync();
                }
            }

            var alreadyInYear = await _context.InstitutionYearSettings
                .AnyAsync(s => s.HebrewYearId == yearId && s.InstitutionId == institution.Id);
            if (alreadyInYear)
                throw new InvalidOperationException("המוסד כבר קיים ברשימת השנה");

            SyncMasterTemplate(institution, name, symbol, typeName, schoolLevel, request.IsSpecialEducation, userId, now);

            _context.InstitutionYearSettings.Add(new InstitutionYearSettings
            {
                EntityId = entityId,
                InstitutionId = institution.Id,
                HebrewYearId = yearId,
                Name = name,
                Symbol = symbol,
                InstitutionType = typeName,
                SchoolLevel = schoolLevel,
                IsSpecialEducation = request.IsSpecialEducation,
                SchoolDaysPerWeek = 0,
                WeeklyHours = 0,
                HasExtendedSchoolDay = false,
                CreatedAt = now,
                UserId = userId,
                UpdatedAt = now,
                UpdateUser = userId
            });

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Created institution year roster {Name} ({Type}) year {YearId} for entity {EntityId}",
                name, typeName, yearId, entityId);
            return institution.Id;
        }

        public async Task UpdateOrgUnitAsync(int entityId, int? userId, int yearId, int id, UpdateOrgUnitRequest request)
        {
            _ = entityId;
            await EnsureYearExistsAsync(yearId);

            if (string.IsNullOrWhiteSpace(request.Name))
                throw new InvalidOperationException("שם מוסד הוא שדה חובה");

            var row = await _context.InstitutionYearSettings
                .FirstOrDefaultAsync(s => s.HebrewYearId == yearId && s.InstitutionId == id)
                ?? throw new InvalidOperationException("מוסד לא נמצא בשנה זו");

            var institution = await _context.Institutions.FirstOrDefaultAsync(i => i.Id == id)
                ?? throw new InvalidOperationException("מוסד לא נמצא");

            var typeName = string.IsNullOrWhiteSpace(request.OrgUnitType)
                ? row.InstitutionType
                : NormalizeType(request.OrgUnitType);
            var schoolLevel = NormalizeSchoolLevel(typeName, request.SchoolLevel);
            var name = request.Name.Trim();
            var symbol = NormalizeSymbol(request.Symbol);

            await EnsureYearNameUniqueAsync(yearId, name, excludeInstitutionId: id);
            if (symbol != null)
                await EnsureYearSymbolUniqueAsync(yearId, symbol, excludeInstitutionId: id);

            var now = DateTime.UtcNow;
            row.Name = name;
            row.Symbol = symbol;
            row.InstitutionType = typeName;
            row.SchoolLevel = schoolLevel;
            row.IsSpecialEducation = request.IsSpecialEducation;
            row.UpdatedAt = now;
            row.UpdateUser = userId;

            SyncMasterTemplate(institution, name, symbol, typeName, schoolLevel, request.IsSpecialEducation, userId, now);

            await _context.SaveChangesAsync();
        }

        /// <summary>Removes the institution from the year's roster (does not delete the master).</summary>
        public async Task RemoveFromYearAsync(int entityId, int? userId, int yearId, int id)
        {
            _ = entityId;
            _ = userId;
            await EnsureYearExistsAsync(yearId);

            var row = await _context.InstitutionYearSettings
                .FirstOrDefaultAsync(s => s.HebrewYearId == yearId && s.InstitutionId == id)
                ?? throw new InvalidOperationException("מוסד לא נמצא בשנה זו");

            var hasEntitlements = await _context.Entitlements.AsNoTracking()
                .AnyAsync(e => e.HebrewYearId == yearId
                               && e.InstitutionId == id
                               && e.IsLastVersion
                               && !e.IsCancelled);
            if (hasEntitlements)
                throw new InvalidOperationException("לא ניתן להסיר מוסד עם זכאויות פעילות בשנה זו");

            _context.InstitutionYearSettings.Remove(row);
            await _context.SaveChangesAsync();
        }

        public async Task<OrgUnitCopyStatusDto> GetCopyStatusAsync(int yearId)
        {
            await EnsureYearExistsAsync(yearId);
            var previous = await GetPreviousYearAsync(yearId);
            var hasRoster = await HasRosterAsync(yearId);

            return new OrgUnitCopyStatusDto
            {
                CanCopy = previous != null && !hasRoster,
                PreviousYearId = previous?.Id,
                PreviousYearName = previous?.YearName,
                HasRoster = hasRoster
            };
        }

        public async Task<(string PreviousYearName, int Imported)> CopyFromPreviousAsync(
            int entityId, int? userId, int yearId)
        {
            await EnsureYearExistsAsync(yearId);

            if (await HasRosterAsync(yearId))
                throw new InvalidOperationException("לשנה זו כבר קיימת רשימת מוסדות");

            var previous = await GetPreviousYearAsync(yearId)
                ?? throw new InvalidOperationException("לא נמצאה שנה קודמת להעתקה");

            var sourceRows = await _context.InstitutionYearSettings
                .AsNoTracking()
                .Where(s => s.HebrewYearId == previous.Id)
                .ToListAsync();

            if (sourceRows.Count == 0)
                throw new InvalidOperationException($"אין מוסדות להעתקה משנת {previous.YearName}");

            var now = DateTime.UtcNow;
            await using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                foreach (var src in sourceRows)
                {
                    _context.InstitutionYearSettings.Add(new InstitutionYearSettings
                    {
                        EntityId = entityId,
                        InstitutionId = src.InstitutionId,
                        HebrewYearId = yearId,
                        Name = src.Name,
                        Symbol = src.Symbol,
                        InstitutionType = src.InstitutionType,
                        SchoolLevel = src.SchoolLevel,
                        IsSpecialEducation = src.IsSpecialEducation,
                        SchoolDaysPerWeek = src.SchoolDaysPerWeek,
                        WeeklyHours = src.WeeklyHours,
                        HasExtendedSchoolDay = src.HasExtendedSchoolDay,
                        CreatedAt = now,
                        UserId = userId,
                        UpdatedAt = now,
                        UpdateUser = userId
                    });
                }

                await _context.SaveChangesAsync();
                await tx.CommitAsync();
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }

            _logger.LogInformation(
                "Copied {Count} institution roster rows from year {PreviousYearId} to {YearId} for entity {EntityId}",
                sourceRows.Count, previous.Id, yearId, entityId);

            return (previous.YearName, sourceRows.Count);
        }

        /// <summary>
        /// Creates or links masters and inserts year roster rows. Caller must ensure year roster is empty.
        /// </summary>
        public async Task<InstitutionRosterUploadResult> ImportRosterRowsAsync(
            int entityId,
            int? userId,
            int yearId,
            List<InstitutionRosterFileRow> rows)
        {
            await EnsureYearExistsAsync(yearId);

            if (await HasRosterAsync(yearId))
                throw new InvalidOperationException("לשנה זו כבר קיימת רשימת מוסדות — העלאה מותרת רק לשנה ריקה");

            var result = new InstitutionRosterUploadResult();
            var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var seenSymbols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var now = DateTime.UtcNow;

            var masters = await _context.Institutions.ToListAsync();
            var mastersBySymbol = masters
                .Where(m => !string.IsNullOrWhiteSpace(m.Symbol))
                .GroupBy(m => NormalizeSymbolKey(m.Symbol!), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            await using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                foreach (var row in rows)
                {
                    try
                    {
                        if (string.IsNullOrWhiteSpace(row.Name))
                        {
                            result.Errors.Add($"שורה {row.RowNumber}: שם מוסד חסר");
                            continue;
                        }

                        string typeName;
                        string? schoolLevel;
                        try
                        {
                            typeName = ParseInstitutionType(row.InstitutionType);
                            schoolLevel = NormalizeSchoolLevel(typeName, ParseSchoolLevel(row.SchoolLevel, typeName));
                        }
                        catch (InvalidOperationException ex)
                        {
                            result.Errors.Add($"שורה {row.RowNumber}: {ex.Message}");
                            continue;
                        }

                        var name = row.Name.Trim();
                        if (!seenNames.Add(name))
                        {
                            result.Errors.Add($"שורה {row.RowNumber}: שם מוסד כפול בקובץ");
                            continue;
                        }

                        string? symbol;
                        try
                        {
                            symbol = NormalizeSymbol(row.Symbol);
                        }
                        catch (InvalidOperationException ex)
                        {
                            result.Errors.Add($"שורה {row.RowNumber}: {ex.Message}");
                            continue;
                        }

                        if (symbol != null)
                        {
                            var symbolKey = NormalizeSymbolKey(symbol);
                            if (!seenSymbols.Add(symbolKey))
                            {
                                result.Errors.Add($"שורה {row.RowNumber}: סמל מוסד כפול בקובץ");
                                continue;
                            }
                        }

                        var isSpecial = ParseBool(row.IsSpecialEducation);

                        Institution institution;
                        var linkedExisting = false;
                        if (symbol != null &&
                            mastersBySymbol.TryGetValue(NormalizeSymbolKey(symbol), out var existingBySymbol))
                        {
                            institution = existingBySymbol;
                            linkedExisting = true;
                            SyncMasterTemplate(institution, name, symbol, typeName, schoolLevel, isSpecial, userId, now);
                        }
                        else
                        {
                            institution = new Institution
                            {
                                EntityId = entityId,
                                Name = name,
                                Symbol = symbol,
                                InstitutionType = typeName,
                                SchoolLevel = schoolLevel,
                                IsSpecialEducation = isSpecial,
                                IsActive = true,
                                UserId = userId,
                                UpdateUser = userId,
                                CreatedAt = now,
                                UpdatedAt = now
                            };
                            _context.Institutions.Add(institution);
                            await _context.SaveChangesAsync();

                            if (symbol != null)
                                mastersBySymbol[NormalizeSymbolKey(symbol)] = institution;
                        }

                        _context.InstitutionYearSettings.Add(new InstitutionYearSettings
                        {
                            EntityId = entityId,
                            InstitutionId = institution.Id,
                            HebrewYearId = yearId,
                            Name = name,
                            Symbol = symbol,
                            InstitutionType = typeName,
                            SchoolLevel = schoolLevel,
                            IsSpecialEducation = isSpecial,
                            SchoolDaysPerWeek = 0,
                            WeeklyHours = 0,
                            HasExtendedSchoolDay = false,
                            CreatedAt = now,
                            UserId = userId,
                            UpdatedAt = now,
                            UpdateUser = userId
                        });

                        if (linkedExisting)
                            result.LinkedExisting++;
                        else
                            result.Created++;
                    }
                    catch (Exception ex)
                    {
                        result.Errors.Add($"שורה {row.RowNumber}: {ex.Message}");
                    }
                }

                if (result.Created + result.LinkedExisting == 0)
                {
                    await tx.RollbackAsync();
                    return result;
                }

                await _context.SaveChangesAsync();
                await tx.CommitAsync();
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }

            return result;
        }

        private async Task EnsureYearNameUniqueAsync(int yearId, string name, int? excludeInstitutionId)
        {
            var exists = await _context.InstitutionYearSettings
                .AnyAsync(s => s.HebrewYearId == yearId
                               && s.Name == name
                               && (excludeInstitutionId == null || s.InstitutionId != excludeInstitutionId));
            if (exists)
                throw new InvalidOperationException("מוסד עם שם זה כבר קיים בשנה זו");
        }

        private async Task EnsureYearSymbolUniqueAsync(int yearId, string symbol, int? excludeInstitutionId)
        {
            var exists = await _context.InstitutionYearSettings
                .AnyAsync(s => s.HebrewYearId == yearId
                               && s.Symbol == symbol
                               && (excludeInstitutionId == null || s.InstitutionId != excludeInstitutionId));
            if (exists)
                throw new InvalidOperationException("מוסד עם סמל זה כבר קיים בשנה זו");
        }

        private async Task<HebrewYear?> GetPreviousYearAsync(int yearId)
        {
            return await _sharedContext.HebrewYears.AsNoTracking()
                .Where(y => y.Id < yearId)
                .OrderByDescending(y => y.Id)
                .FirstOrDefaultAsync();
        }

        private static void SyncMasterTemplate(
            Institution institution,
            string name,
            string? symbol,
            string typeName,
            string? schoolLevel,
            bool isSpecialEducation,
            int? userId,
            DateTime now)
        {
            institution.Name = name;
            institution.Symbol = symbol ?? institution.Symbol;
            institution.InstitutionType = typeName;
            institution.SchoolLevel = schoolLevel;
            institution.IsSpecialEducation = isSpecialEducation;
            institution.IsActive = true;
            institution.UpdateUser = userId;
            institution.UpdatedAt = now;
        }

        private static OrgUnitDto MapDto(InstitutionYearSettings s) => new()
        {
            Id = s.InstitutionId,
            Name = s.Name,
            Symbol = s.Symbol,
            OrgUnitType = s.InstitutionType,
            OrgUnitTypeDescription = GetTypeDescription(s.InstitutionType),
            SchoolLevel = s.SchoolLevel,
            IsSpecialEducation = s.IsSpecialEducation,
            IsActive = true
        };

        internal static string? NormalizeSymbol(string? symbol)
        {
            if (string.IsNullOrWhiteSpace(symbol))
                return null;
            var trimmed = symbol.Trim();
            if (trimmed.EndsWith(".0", StringComparison.Ordinal) &&
                trimmed.Length > 2 &&
                trimmed.AsSpan(0, trimmed.Length - 2).ToString().All(char.IsDigit))
            {
                trimmed = trimmed[..^2];
            }
            if (trimmed.Length > 20)
                throw new InvalidOperationException("סמל מוסד ארוך מדי");
            return trimmed;
        }

        internal static string NormalizeSymbolKey(string symbol) =>
            NormalizeSymbol(symbol) ?? symbol.Trim();

        private static string GetTypeDescription(string type) => type switch
        {
            InstitutionTypes.School => "בית ספר",
            InstitutionTypes.Kindergarten => "גן ילדים",
            _ => type
        };

        internal static string NormalizeType(string? orgUnitType)
        {
            var typeName = (orgUnitType ?? string.Empty).Trim().ToLowerInvariant();
            if (!AllowedTypes.Contains(typeName))
                throw new InvalidOperationException("סוג מוסד חייב להיות בית ספר או גן");
            return typeName;
        }

        internal static string ParseInstitutionType(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                throw new InvalidOperationException("סוג מוסד חסר");

            var v = raw.Trim().ToLowerInvariant();
            if (v is "school" or "בית ספר" or "ביס" or "בס")
                return InstitutionTypes.School;
            if (v is "kindergarten" or "גן" or "גן ילדים" or "גנים")
                return InstitutionTypes.Kindergarten;

            return NormalizeType(v);
        }

        internal static string? ParseSchoolLevel(string? raw, string institutionType)
        {
            if (institutionType == InstitutionTypes.Kindergarten)
                return null;
            if (string.IsNullOrWhiteSpace(raw))
                return null;

            var v = raw.Trim().ToLowerInvariant();
            if (v.Contains("elementary") || v.Contains("יסודי"))
                return SchoolLevels.Elementary;
            if (v.Contains("high") || v.Contains("חט") || v.Contains("עליונה") || v.Contains("תיכון"))
                return SchoolLevels.HighSchool;

            return raw.Trim().ToLowerInvariant();
        }

        internal static bool ParseBool(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return false;
            var v = raw.Trim().ToLowerInvariant();
            return v is "true" or "1" or "yes" or "y" or "כן" or "חיובי";
        }

        internal static string? NormalizeSchoolLevel(string institutionType, string? schoolLevel)
        {
            if (institutionType == InstitutionTypes.Kindergarten)
            {
                if (!string.IsNullOrWhiteSpace(schoolLevel))
                    throw new InvalidOperationException("רמת בית ספר אינה רלוונטית לגן ילדים");
                return null;
            }

            var level = (schoolLevel ?? string.Empty).Trim().ToLowerInvariant();
            if (!AllowedSchoolLevels.Contains(level))
                throw new InvalidOperationException(
                    $"רמת בית ספר חייבת להיות {SchoolLevels.ElementaryDisplay} או {SchoolLevels.HighSchoolDisplay}");

            return level;
        }
    }

    public class InstitutionRosterFileRow
    {
        public int RowNumber { get; set; }
        public string? Symbol { get; set; }
        public string? Name { get; set; }
        public string? InstitutionType { get; set; }
        public string? SchoolLevel { get; set; }
        public string? IsSpecialEducation { get; set; }
    }

    public class InstitutionRosterUploadResult
    {
        public int Created { get; set; }
        public int LinkedExisting { get; set; }
        public List<string> Errors { get; set; } = new();
    }
}
