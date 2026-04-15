using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetelATH.Api.Data;
using PetelATH.Api.Session;

namespace PetelATH.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PersonsController : BaseController
    {
        private readonly AppDbContext _context;

        public PersonsController(
            AppDbContext context,
            UserSessionService userSessionService,
            ILogger<PersonsController> logger)
            : base(userSessionService, logger)
        {
            _context = context;
        }

        /// <summary>
        /// Get person details by ID
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetPerson(int id)
        {
            try
            {
                var person = await _context.Persons
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.Id == id);

                if (person == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "אדם לא נמצא"
                    });
                }

                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        id = person.Id,
                        firstName = person.FirstName,
                        lastName = person.LastName,
                        phoneNumberPrefix = person.PhoneNumberPrefix,
                        phoneNumber = person.PhoneNumber,
                        email = person.Email
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting person {PersonId}", id);
                return StatusCode(500, new
                {
                    success = false,
                    message = "שגיאה בטעינת פרטי אדם"
                });
            }
        }

        /// <summary>
        /// Create new person
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> CreatePerson([FromBody] CreatePersonDto dto)
        {
            try
            {
                _logger.LogInformation("Creating new person: {FirstName} {LastName}", 
                    dto.FirstName, dto.LastName);

                var person = new Person
                {
                    IdNumber = string.IsNullOrWhiteSpace(dto.IdNumber) ? "0" : dto.IdNumber,
                    IdType = dto.IdType,
                    FirstName = dto.FirstName,
                    LastName = dto.LastName,
                    PhoneNumberPrefix = dto.PhoneNumberPrefix,
                    PhoneNumber = dto.PhoneNumber,
                    Email = dto.Email
                };

                _context.Persons.Add(person);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Person created successfully with ID: {PersonId}", person.Id);

                return Ok(new
                {
                    success = true,
                    message = "אדם נוצר בהצלחה",
                    data = new
                    {
                            id = person.Id,
                            idNumber = person.IdNumber,
                            idType = person.IdType,
                            firstName = person.FirstName,
                            lastName = person.LastName,
                            phoneNumberPrefix = person.PhoneNumberPrefix,
                            phoneNumber = person.PhoneNumber,
                            email = person.Email
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating person");
                return StatusCode(500, new
                {
                    success = false,
                    message = "שגיאה ביצירת אדם"
                });
            }
        }

        /// <summary>
        /// Update person contact details (phone/email only)
        /// </summary>
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdatePerson(int id, [FromBody] UpdatePersonDto dto)
        {
            try
            {
                _logger.LogInformation("Updating person {PersonId}", id);

                var person = await _context.Persons.FindAsync(id);

                if (person == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "אדם לא נמצא"
                    });
                }

                // Update only editable fields
                person.IdNumber = string.IsNullOrWhiteSpace(dto.IdNumber) ? person.IdNumber : dto.IdNumber;
                person.IdType = dto.IdType;
                person.PhoneNumberPrefix = dto.PhoneNumberPrefix;
                person.PhoneNumber = dto.PhoneNumber;
                person.Email = dto.Email;


                await _context.SaveChangesAsync();

                _logger.LogInformation("Person {PersonId} updated successfully", id);

                return Ok(new
                {
                    success = true,
                    message = "פרטי אדם עודכנו בהצלחה"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating person {PersonId}", id);
                return StatusCode(500, new
                {
                    success = false,
                    message = "שגיאה בעדכון פרטי אדם"
                });
            }
        }

        /// <summary>
        /// Search for persons by name (partial match)
        /// Used by person selection modals in school details
        /// </summary>
        [HttpGet("search")]
        public async Task<IActionResult> SearchPersons(
            [FromQuery] string? firstName = null,
            [FromQuery] string? lastName = null)
        {
            try
            {
                _logger.LogInformation("Searching persons: FirstName={FirstName}, LastName={LastName}", 
                    firstName, lastName);

                var query = _context.Persons.AsQueryable();

                // Apply filters if provided
                if (!string.IsNullOrWhiteSpace(firstName))
                {
                    query = query.Where(p => p.FirstName.Contains(firstName));
                }

                if (!string.IsNullOrWhiteSpace(lastName))
                {
                    query = query.Where(p => p.LastName.Contains(lastName));
                }

                var persons = await query
                    .Select(p => new
                    {
                        id = p.Id,
                        firstName = p.FirstName,
                        lastName = p.LastName,
                        position = p.Position,
                        phoneNumberPrefix = p.PhoneNumberPrefix,
                        phoneNumber = p.PhoneNumber,
                        email = p.Email
                    })
                    .Take(50) // Limit results to prevent large response
                    .ToListAsync();

                _logger.LogInformation("Found {Count} persons matching search criteria", persons.Count);

                return Ok(new
                {
                    success = true,
                    data = persons
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching persons");
                return StatusCode(500, new
                {
                    success = false,
                    message = "שגיאה בחיפוש אנשים"
                });
            }
        }
    }

    public class CreatePersonDto
    {
        public string? IdNumber { get; set; }
        public int IdType { get; set; } = 0;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string? PhoneNumberPrefix { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Email { get; set; }

        public string? Position { get; set; }

    }

    public class UpdatePersonDto
    {

        public int Id { get; set; }
        public string? IdNumber { get; set; }
        public int IdType { get; set; } 
        public string? PhoneNumberPrefix { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Email { get; set; }
    }
}