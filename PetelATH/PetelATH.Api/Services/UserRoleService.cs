using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PetelATH.Api.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PetelATH.Api.Services
{
    /// <summary>
    /// Service for managing user roles following the Entity-Based Request Flow pattern
    /// </summary>
    public class UserRoleService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<UserRoleService> _logger;

        public UserRoleService(AppDbContext context, ILogger<UserRoleService> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Gets all roles for a user
        /// </summary>
        /// <param name="userId">The user ID</param>
        /// <returns>List of role names</returns>
        public async Task<List<int>> GetUserRolesAsync(int userId)
        {
            try
            {
                var userRoles = await _context.UserRoles
                    .Include(ur => ur.Role)
                    .Where(ur => ur.UserId == userId)
                    .Select(ur => ur.Role.Id)
                    .ToListAsync();

                _logger.LogInformation("Retrieved {Count} roles for user {UserId}", userRoles.Count, userId);
                return userRoles;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving roles for user {UserId}", userId);
                return new List<int>();
            }
        }

        /// <summary>
        /// Checks if a user has a specific role
        /// </summary>
        /// <param name="userId">The user ID</param>
        /// <param name="roleName">The role name to check</param>
        /// <returns>True if the user has the role, false otherwise</returns>
        public async Task<bool> UserHasRoleAsync(int userId, string roleName)
        {
            try
            {
                var hasRole = await _context.UserRoles
                    .Include(ur => ur.Role)
                    .AnyAsync(ur => ur.UserId == userId && ur.Role.Name == roleName);

                return hasRole;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking role {RoleName} for user {UserId}", roleName, userId);
                return false;
            }
        }

        /// <summary>
        /// Assigns a role to a user
        /// </summary>
        /// <param name="userId">The user ID</param>
        /// <param name="roleId">The role ID</param>
        /// <returns>True if successful, false otherwise</returns>
        public async Task<bool> AssignRoleToUserAsync(int userId, int roleId)
        {
            try
            {
                // Check if already assigned
                bool alreadyAssigned = await _context.UserRoles
                    .AnyAsync(ur => ur.UserId == userId && ur.RoleId == roleId);

                if (alreadyAssigned)
                {
                    return true;
                }

                // Add new role assignment
                var userRole = new UserRole
                {
                    UserId = userId,
                    RoleId = roleId,
                    CreatedAt = DateTime.UtcNow
                };

                _context.UserRoles.Add(userRole);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Role {RoleId} assigned to user {UserId}", roleId, userId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error assigning role {RoleId} to user {UserId}", roleId, userId);
                return false;
            }
        }

        /// <summary>
        /// Removes a role from a user
        /// </summary>
        /// <param name="userId">The user ID</param>
        /// <param name="roleId">The role ID</param>
        /// <returns>True if successful, false otherwise</returns>
        public async Task<bool> RemoveRoleFromUserAsync(int userId, int roleId)
        {
            try
            {
                var userRole = await _context.UserRoles
                    .FirstOrDefaultAsync(ur => ur.UserId == userId && ur.RoleId == roleId);

                if (userRole == null)
                {
                    return true;
                }

                _context.UserRoles.Remove(userRole);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Role {RoleId} removed from user {UserId}", roleId, userId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing role {RoleId} from user {UserId}", roleId, userId);
                return false;
            }
        }
    }
}