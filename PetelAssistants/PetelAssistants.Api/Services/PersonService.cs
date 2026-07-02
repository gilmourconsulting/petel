using Microsoft.EntityFrameworkCore;
using PetelAssistants.Api.Data;
using PetelAssistants.Api.DTOs;
using PetelAssistants.Api.Models;

namespace PetelAssistants.Api.Services
{
    public class PersonService
    {
        private readonly AssistDbContext _context;
        private readonly SharedDbContext _sharedContext;
        private readonly ILogger<PersonService> _logger;

        public PersonService(
            AssistDbContext context,
            SharedDbContext sharedContext,
            ILogger<PersonService> logger)
        {
            _context = context;
            _sharedContext = sharedContext;
            _logger = logger;
        }

        public async Task<List<PhoneTypeDto>> GetPhoneTypesAsync()
        {
            return await _sharedContext.PhoneTypes
                .AsNoTracking()
                .Where(pt => pt.IsActive)
                .OrderBy(pt => pt.SortOrder)
                .Select(pt => new PhoneTypeDto
                {
                    Id = pt.Id,
                    Code = pt.Code,
                    DisplayName = pt.DisplayName
                })
                .ToListAsync();
        }

        public async Task<bool> IdNumberExistsAsync(int entityId, string idNumber, int? excludePersonId = null)
        {
            var query = _context.Persons.AsNoTracking().Where(p => p.EntityId == entityId);
            if (excludePersonId.HasValue)
                query = query.Where(p => p.Id != excludePersonId.Value);

            var persons = await query.Select(p => new { p.Id, p.IdNumber }).ToListAsync();
            return persons.Any(p => p.IdNumber == idNumber);
        }

        public async Task<int?> CreatePersonAsync(int entityId, int? userId, CreatePersonRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.IdNumber))
                throw new InvalidOperationException("מספר זהות נדרש");

            if (await IdNumberExistsAsync(entityId, request.IdNumber.Trim()))
                throw new InvalidOperationException("מספר זהות כבר קיים ברשות זו");

            var effectiveDate = request.EffectiveDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
            var now = DateTime.UtcNow;

            var person = new Person
            {
                EntityId = entityId,
                IdNumber = request.IdNumber.Trim(),
                IdType = request.IdType,
                UserId = userId,
                UpdateUser = userId,
                CreatedAt = now,
                UpdatedAt = now
            };

            _context.Persons.Add(person);
            await _context.SaveChangesAsync();

            var detail = new PersonDetail
            {
                EntityId = entityId,
                PersonId = person.Id,
                Version = 0,
                IsLastVersion = true,
                StartDate = effectiveDate,
                FirstName = request.FirstName.Trim(),
                LastName = request.LastName.Trim(),
                Gender = request.Gender,
                DateOfBirth = request.DateOfBirth,
                Email = request.Email,
                Position = request.Position,
                UserId = userId,
                UpdateUser = userId,
                CreatedAt = now,
                UpdatedAt = now
            };
            _context.PersonDetails.Add(detail);

            if (request.Address != null && HasAddressData(request.Address))
            {
                _context.PersonAddresses.Add(new PersonAddress
                {
                    EntityId = entityId,
                    PersonId = person.Id,
                    Street = request.Address.Street,
                    HouseNumber = request.Address.HouseNumber,
                    City = request.Address.City,
                    PostCode = request.Address.PostCode,
                    IsActive = true,
                    StartDate = effectiveDate,
                    UserId = userId,
                    UpdateUser = userId,
                    CreatedAt = now,
                    UpdatedAt = now
                });
            }

            foreach (var phone in request.Phones.Where(p => !string.IsNullOrWhiteSpace(p.PhoneNumber)))
            {
                _context.PersonPhones.Add(new PersonPhone
                {
                    EntityId = entityId,
                    PersonId = person.Id,
                    PhoneTypeId = phone.PhoneTypeId,
                    PhoneNumberPrefix = phone.PhoneNumberPrefix,
                    PhoneNumber = phone.PhoneNumber,
                    IsActive = true,
                    StartDate = effectiveDate,
                    UserId = userId,
                    UpdateUser = userId,
                    CreatedAt = now,
                    UpdatedAt = now
                });
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation("Created person {PersonId} for entity {EntityId}", person.Id, entityId);
            return person.Id;
        }

        public async Task<bool> UpdatePersonAsync(int personId, int entityId, int? userId, UpdatePersonRequest request)
        {
            var person = await _context.Persons.FirstOrDefaultAsync(p => p.Id == personId);
            if (person == null)
                return false;

            if (!string.IsNullOrWhiteSpace(request.IdNumber) &&
                request.IdNumber.Trim() != person.IdNumber &&
                await IdNumberExistsAsync(entityId, request.IdNumber.Trim(), personId))
            {
                throw new InvalidOperationException("מספר זהות כבר קיים ברשות זו");
            }

            var effectiveDate = request.EffectiveDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
            var now = DateTime.UtcNow;

            if (!string.IsNullOrWhiteSpace(request.IdNumber))
            {
                person.IdNumber = request.IdNumber.Trim();
                person.IdType = request.IdType ?? person.IdType;
                person.UpdateUser = userId;
                person.UpdatedAt = now;
            }

            var currentDetail = await _context.PersonDetails
                .FirstOrDefaultAsync(d => d.PersonId == personId && d.IsLastVersion);

            if (currentDetail == null)
                throw new InvalidOperationException("לא נמצאה גרסה פעילה לפרטי האדם");

            var detailChanged = currentDetail.FirstName != request.FirstName.Trim() ||
                                currentDetail.LastName != request.LastName.Trim() ||
                                currentDetail.Gender != request.Gender ||
                                currentDetail.DateOfBirth != request.DateOfBirth ||
                                currentDetail.Email != request.Email ||
                                currentDetail.Position != request.Position;

            if (detailChanged)
            {
                currentDetail.IsLastVersion = false;
                currentDetail.EndDate = effectiveDate <= currentDetail.StartDate
                    ? currentDetail.StartDate
                    : effectiveDate.AddDays(-1);
                currentDetail.UpdateUser = userId;
                currentDetail.UpdatedAt = now;

                _context.PersonDetails.Add(new PersonDetail
                {
                    EntityId = entityId,
                    PersonId = personId,
                    Version = currentDetail.Version + 1,
                    IsLastVersion = true,
                    StartDate = effectiveDate,
                    FirstName = request.FirstName.Trim(),
                    LastName = request.LastName.Trim(),
                    Gender = request.Gender,
                    DateOfBirth = request.DateOfBirth,
                    Email = request.Email,
                    Position = request.Position,
                    UserId = userId,
                    UpdateUser = userId,
                    CreatedAt = now,
                    UpdatedAt = now
                });
            }

            if (request.Address != null)
            {
                await UpsertActiveAddressAsync(personId, entityId, userId, request.Address, effectiveDate, now);
            }

            foreach (var phone in request.Phones)
            {
                if (string.IsNullOrWhiteSpace(phone.PhoneNumber))
                    continue;

                await UpsertActivePhoneAsync(
                    personId, entityId, userId,
                    phone.PhoneTypeId,
                    phone.PhoneNumberPrefix,
                    phone.PhoneNumber,
                    effectiveDate, now);
            }

            await _context.SaveChangesAsync();
            return true;
        }

        private async Task UpsertActiveAddressAsync(
            int personId, int entityId, int? userId,
            PersonAddressInputDto input, DateOnly effectiveDate, DateTime now)
        {
            if (!HasAddressData(input))
                return;

            var current = await _context.PersonAddresses
                .FirstOrDefaultAsync(a => a.PersonId == personId && a.IsActive);

            if (current != null &&
                current.Street == input.Street &&
                current.HouseNumber == input.HouseNumber &&
                current.City == input.City &&
                current.PostCode == input.PostCode)
            {
                return;
            }

            if (current != null)
            {
                current.IsActive = false;
                current.EndDate = effectiveDate <= current.StartDate
                    ? current.StartDate
                    : effectiveDate.AddDays(-1);
                current.UpdateUser = userId;
                current.UpdatedAt = now;
            }

            _context.PersonAddresses.Add(new PersonAddress
            {
                EntityId = entityId,
                PersonId = personId,
                Street = input.Street,
                HouseNumber = input.HouseNumber,
                City = input.City,
                PostCode = input.PostCode,
                IsActive = true,
                StartDate = effectiveDate,
                UserId = userId,
                UpdateUser = userId,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        private async Task UpsertActivePhoneAsync(
            int personId, int entityId, int? userId,
            int phoneTypeId, string? prefix, string? number,
            DateOnly effectiveDate, DateTime now)
        {
            var current = await _context.PersonPhones
                .FirstOrDefaultAsync(p => p.PersonId == personId && p.PhoneTypeId == phoneTypeId && p.IsActive);

            if (current != null &&
                current.PhoneNumberPrefix == prefix &&
                current.PhoneNumber == number)
            {
                return;
            }

            if (current != null)
            {
                current.IsActive = false;
                current.EndDate = effectiveDate <= current.StartDate
                    ? current.StartDate
                    : effectiveDate.AddDays(-1);
                current.UpdateUser = userId;
                current.UpdatedAt = now;
            }

            _context.PersonPhones.Add(new PersonPhone
            {
                EntityId = entityId,
                PersonId = personId,
                PhoneTypeId = phoneTypeId,
                PhoneNumberPrefix = prefix,
                PhoneNumber = number,
                IsActive = true,
                StartDate = effectiveDate,
                UserId = userId,
                UpdateUser = userId,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        public async Task<List<PersonListItemDto>> ListPersonsAsync()
        {
            var phoneTypes = await GetPhoneTypeMapAsync();

            var persons = await _context.Persons
                .AsNoTracking()
                .Include(p => p.Details.Where(d => d.IsLastVersion))
                .Include(p => p.Phones.Where(ph => ph.IsActive))
                .OrderBy(p => p.Id)
                .ToListAsync();

            return persons.Select(p =>
            {
                var detail = p.Details.FirstOrDefault();
                var phones = p.Phones
                    .Select(ph => FormatPhone(ph, phoneTypes))
                    .Where(s => !string.IsNullOrEmpty(s))
                    .ToList();

                return new PersonListItemDto
                {
                    Id = p.Id,
                    IdNumber = MaskIdNumber(p.IdNumber),
                    IdType = p.IdType,
                    FirstName = detail?.FirstName ?? string.Empty,
                    LastName = detail?.LastName ?? string.Empty,
                    FullName = $"{detail?.FirstName} {detail?.LastName}".Trim(),
                    PhoneSummary = phones.Count > 0 ? string.Join(", ", phones) : null,
                    UpdatedAt = p.UpdatedAt
                };
            }).OrderBy(p => p.LastName).ThenBy(p => p.FirstName).ToList();
        }

        public async Task<List<PersonListItemDto>> SearchPersonsAsync(string term)
        {
            term = term.Trim();
            if (string.IsNullOrEmpty(term))
                return await ListPersonsAsync();

            var phoneTypes = await GetPhoneTypeMapAsync();

            var byId = await _context.Persons
                .AsNoTracking()
                .Where(p => p.IdNumber == term)
                .Include(p => p.Details.Where(d => d.IsLastVersion))
                .Include(p => p.Phones.Where(ph => ph.IsActive))
                .ToListAsync();

            if (byId.Count > 0)
                return MapListItems(byId, phoneTypes);

            var lower = term.ToLower();
            var byName = await _context.PersonDetails
                .AsNoTracking()
                .Where(d => d.IsLastVersion &&
                            (d.FirstName.ToLower().Contains(lower) || d.LastName.ToLower().Contains(lower)))
                .Include(d => d.Person)
                    .ThenInclude(p => p!.Phones.Where(ph => ph.IsActive))
                .Take(50)
                .ToListAsync();

            return byName
                .Select(d => d.Person)
                .Where(p => p != null)
                .DistinctBy(p => p!.Id)
                .Select(p => MapListItem(p!, phoneTypes))
                .OrderBy(p => p.LastName)
                .ThenBy(p => p.FirstName)
                .ToList();
        }

        public async Task<PersonSnapshotDto?> GetPersonSnapshotAsync(int personId)
        {
            var phoneTypes = await GetPhoneTypeMapAsync();

            var person = await _context.Persons
                .AsNoTracking()
                .Include(p => p.Details.Where(d => d.IsLastVersion))
                .Include(p => p.Addresses.Where(a => a.IsActive))
                .Include(p => p.Phones.Where(ph => ph.IsActive))
                .FirstOrDefaultAsync(p => p.Id == personId);

            if (person == null)
                return null;

            return MapSnapshot(person, phoneTypes);
        }

        public async Task<List<PersonDetailHistoryDto>> GetDetailHistoryAsync(int personId)
        {
            return await _context.PersonDetails
                .AsNoTracking()
                .Where(d => d.PersonId == personId)
                .OrderByDescending(d => d.Version)
                .Select(d => new PersonDetailHistoryDto
                {
                    Id = d.Id,
                    Version = d.Version,
                    IsLastVersion = d.IsLastVersion,
                    StartDate = d.StartDate,
                    EndDate = d.EndDate,
                    FirstName = d.FirstName,
                    LastName = d.LastName,
                    Gender = d.Gender,
                    DateOfBirth = d.DateOfBirth,
                    Email = d.Email,
                    Position = d.Position
                })
                .ToListAsync();
        }

        private async Task<Dictionary<int, PhoneType>> GetPhoneTypeMapAsync()
        {
            return await _sharedContext.PhoneTypes
                .AsNoTracking()
                .ToDictionaryAsync(pt => pt.Id);
        }

        private static bool HasAddressData(PersonAddressInputDto input) =>
            !string.IsNullOrWhiteSpace(input.Street) ||
            !string.IsNullOrWhiteSpace(input.HouseNumber) ||
            !string.IsNullOrWhiteSpace(input.City) ||
            !string.IsNullOrWhiteSpace(input.PostCode);

        private static string MaskIdNumber(string idNumber)
        {
            if (string.IsNullOrEmpty(idNumber) || idNumber.Length <= 4)
                return idNumber;
            return new string('*', idNumber.Length - 4) + idNumber[^4..];
        }

        private static List<PersonListItemDto> MapListItems(List<Person> persons, Dictionary<int, PhoneType> phoneTypes) =>
            persons.Select(p => MapListItem(p, phoneTypes)).ToList();

        private static PersonListItemDto MapListItem(Person person, Dictionary<int, PhoneType> phoneTypes)
        {
            var detail = person.Details.FirstOrDefault(d => d.IsLastVersion) ?? person.Details.FirstOrDefault();
            var phones = person.Phones
                .Where(ph => ph.IsActive)
                .Select(ph => FormatPhone(ph, phoneTypes))
                .Where(s => !string.IsNullOrEmpty(s))
                .ToList();

            return new PersonListItemDto
            {
                Id = person.Id,
                IdNumber = MaskIdNumber(person.IdNumber),
                IdType = person.IdType,
                FirstName = detail?.FirstName ?? string.Empty,
                LastName = detail?.LastName ?? string.Empty,
                FullName = $"{detail?.FirstName} {detail?.LastName}".Trim(),
                PhoneSummary = phones.Count > 0 ? string.Join(", ", phones) : null,
                UpdatedAt = person.UpdatedAt
            };
        }

        private static PersonSnapshotDto MapSnapshot(Person person, Dictionary<int, PhoneType> phoneTypes)
        {
            var detail = person.Details.FirstOrDefault(d => d.IsLastVersion)!;
            var address = person.Addresses.FirstOrDefault(a => a.IsActive);

            return new PersonSnapshotDto
            {
                Id = person.Id,
                IdNumber = person.IdNumber,
                IdType = person.IdType,
                DetailId = detail.Id,
                Version = detail.Version,
                StartDate = detail.StartDate,
                EndDate = detail.EndDate,
                FirstName = detail.FirstName,
                LastName = detail.LastName,
                FullName = $"{detail.FirstName} {detail.LastName}".Trim(),
                Gender = detail.Gender,
                DateOfBirth = detail.DateOfBirth,
                Email = detail.Email,
                Position = detail.Position,
                Address = address == null ? null : new PersonAddressDto
                {
                    Id = address.Id,
                    Street = address.Street,
                    HouseNumber = address.HouseNumber,
                    City = address.City,
                    PostCode = address.PostCode,
                    IsActive = address.IsActive,
                    StartDate = address.StartDate,
                    EndDate = address.EndDate
                },
                Phones = person.Phones
                    .Where(ph => ph.IsActive)
                    .Select(ph => new PersonPhoneDto
                    {
                        Id = ph.Id,
                        PhoneTypeId = ph.PhoneTypeId,
                        PhoneTypeCode = phoneTypes.TryGetValue(ph.PhoneTypeId, out var pt) ? pt.Code : string.Empty,
                        PhoneTypeDisplayName = phoneTypes.TryGetValue(ph.PhoneTypeId, out var pt2) ? pt2.DisplayName : string.Empty,
                        PhoneNumberPrefix = ph.PhoneNumberPrefix,
                        PhoneNumber = ph.PhoneNumber,
                        IsActive = ph.IsActive,
                        StartDate = ph.StartDate,
                        EndDate = ph.EndDate
                    })
                    .OrderBy(ph => ph.PhoneTypeId)
                    .ToList(),
                UpdatedAt = person.UpdatedAt
            };
        }

        private static string FormatPhone(PersonPhone phone, Dictionary<int, PhoneType> phoneTypes)
        {
            if (string.IsNullOrWhiteSpace(phone.PhoneNumber))
                return string.Empty;

            var label = phoneTypes.TryGetValue(phone.PhoneTypeId, out var pt) ? pt.DisplayName : string.Empty;
            var number = $"{phone.PhoneNumberPrefix}{phone.PhoneNumber}".Trim();
            return string.IsNullOrEmpty(label) ? number : $"{label}: {number}";
        }
    }
}
