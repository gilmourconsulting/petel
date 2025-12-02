using System.Text.RegularExpressions;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PetelApp.Api.Data;

namespace PetelApp.Api.Services
{
    /// <summary>
    /// Global utility functions for Petel Educational Management System
    /// Reusable helper methods for text processing and data retrieval
    /// </summary>
    public class GlobalFunctions
    {
        private readonly AppDbContext _context;

        public GlobalFunctions(AppDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Extracts only Hebrew letters and numbers from a string
        /// Removes spaces, dashes, and other non-Hebrew/non-numeric characters
        /// </summary>
        /// <param name="text">Input string</param>
        /// <returns>String containing only Hebrew letters (א-ת) and digits (0-9)</returns>
        public static string PureHebrewText(string? text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            // Hebrew Unicode range for letters: \u05D0-\u05EA (א-ת)
            // Digits: 0-9
            return Regex.Replace(text, @"[^\u05D0-\u05EA0-9]", string.Empty);
        }

        /// <summary>
        /// Gets school year ID by year_id and school_id
        /// </summary>
        /// <param name="yearId">Hebrew year ID</param>
        /// <param name="schoolId">School entity ID</param>
        /// <returns>School year ID or null if not found</returns>
        public async Task<int?> GetSchoolYearByIds(int yearId, int schoolId)
        {
            var schoolYear = await _context.SchoolYears
                .Where(sy => sy.YearId == yearId && sy.SchoolId == schoolId)
                .Select(sy => sy.Id)
                .FirstOrDefaultAsync();

            return schoolYear == 0 ? null : schoolYear;
        }

        /// <summary>
        /// Gets school year ID by Hebrew year text and school symbol
        /// </summary>
        /// <param name="hebrewYear">Hebrew year as text (e.g., "תשפ״ה")</param>
        /// <param name="schoolSymbol">School symbol/code from entities table</param>
        /// <returns>School year ID or null if not found</returns>
        public async Task<int?> GetSchoolYearByHebrewYearAndSymbol(string hebrewYear, string schoolSymbol)
        {
            // Step 1: Get Hebrew year ID by year name
            var year = await _context.HebrewYears
                .Where(y => y.HebrewYearText == hebrewYear)
                .Select(y => y.Id)
                .FirstOrDefaultAsync();

            if (year == 0)
                return null;

            // Step 2: Get school ID by symbol from entities table
            var school = await _context.Entities
                .Where(e => e.Symbol == schoolSymbol)
                .Select(e => e.Id)
                .FirstOrDefaultAsync();

            if (school == 0)
                return null;

            // Step 3: Get school year using the two IDs
            return await GetSchoolYearByIds(year, school);
        }

        /// <summary>
        /// Gets class ID by class name and school year
        /// Compares pure Hebrew text of class names
        /// </summary>
        /// <param name="className">Class name (e.g., "א-1", "ב׳")</param>
        /// <param name="schoolYearId">School year ID</param>
        /// <returns>Class ID or null if not found</returns>
        public async Task<int?> GetClassIdByName(string className, int schoolYearId)
        {
            var pureInputName = PureHebrewText(className);

            var classes = await _context.SchoolClasses
                .Where(c => c.SchoolYearId == schoolYearId)
                .ToListAsync();

            var matchedClass = classes
                .FirstOrDefault(c => PureHebrewText(c.Name) == pureInputName);

            return matchedClass?.Id;
        }

        /// <summary>
        /// Gets council ID by council name
        /// Compares pure Hebrew text of council short names
        /// </summary>
        /// <param name="councilName">Council name</param>
        /// <returns>Council ID or null if not found</returns>
        public async Task<int?> GetCouncilByName(string councilName)
        {
            var pureInputName = PureHebrewText(councilName);

            var councils = await _context.Councils.ToListAsync();

            var matchedCouncil = councils
                .FirstOrDefault(c => PureHebrewText(c.CouncilShortName) == pureInputName);

            return matchedCouncil?.Id;
        }

        /// <summary>
        /// Gets council ID by council code
        /// </summary>
        /// <param name="councilCode">Council code</param>
        /// <returns>Council ID or null if not found</returns>
        public async Task<int?> GetCouncilByCode(string councilCode)
        {
            var council = await _context.Councils
                .Where(c => c.CouncilCode.ToString() == councilCode)
                .FirstOrDefaultAsync();

            return council?.Id;
        }

        /// <summary>
        /// Convert Latin parentheses to Hebrew/RTL equivalents for proper display
        /// </summary>
        public static string ToRtlText(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            
            return text
                .Replace("(", "\u200F(") // Add RTL mark before opening parenthesis
                .Replace(")", ")\u200F"); // Add RTL mark after closing parenthesis
        }

                /// <summary>
        /// Format person name from Person entity
        /// </summary>
        public static string FormatPersonName(Person? person)
        {
            if (person == null)
            {
                return string.Empty;
            }

            var firstName = person.FirstName?.Trim() ?? string.Empty;
            var lastName = person.LastName?.Trim() ?? string.Empty;

            return $"{firstName} {lastName}".Trim();
        }
    }
}