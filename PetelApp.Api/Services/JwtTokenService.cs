using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using PetelApp.Api.Configuration;
using PetelApp.Api.Session;

namespace PetelApp.Api.Services
{
    /// <summary>
    /// Service for generating and validating JWT tokens for session authentication
    /// </summary>
    public class JwtTokenService
    {
        private readonly SecuritySettings.JwtSettings _jwtSettings;
        private readonly ILogger<JwtTokenService> _logger;
        private readonly TokenValidationParameters _tokenValidationParameters;

        public JwtTokenService(
            IOptions<SecuritySettings> securitySettings,
            ILogger<JwtTokenService> logger)
        {
            _jwtSettings = securitySettings.Value.Jwt;
            _logger = logger;

            // Validate configuration
            if (string.IsNullOrEmpty(_jwtSettings.SecretKey) || _jwtSettings.SecretKey.Length < 32)
            {
                throw new InvalidOperationException(
                    "JWT SecretKey must be at least 32 characters long. Configure in appsettings.json Security:Jwt:SecretKey");
            }

            // Setup token validation parameters
            _tokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(_jwtSettings.SecretKey)),
                ValidateIssuer = true,
                ValidIssuer = _jwtSettings.Issuer,
                ValidateAudience = true,
                ValidAudience = _jwtSettings.Audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(5)
            };
        }

        /// <summary>
        /// Generate a JWT token for authenticated user session
        /// </summary>
        public string GenerateSessionToken(UserSession session)
        {
            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, session.UserId),
                new Claim(JwtRegisteredClaimNames.Jti, session.SessionId), // Session ID as JWT ID
                new Claim("username", session.Username),
                new Claim("entityId", session.EntityId),
                new Claim("entityName", session.EntityName),
                new Claim(JwtRegisteredClaimNames.Iat, 
                    DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString()),
                
                // Add roles as comma-separated string
                new Claim("roles", string.Join(",", session.Roles ?? new List<int>()))
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.SecretKey));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddHours(_jwtSettings.ExpirationHours),
                Issuer = _jwtSettings.Issuer,
                Audience = _jwtSettings.Audience,
                SigningCredentials = credentials
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            var token = tokenHandler.CreateToken(tokenDescriptor);
            var tokenString = tokenHandler.WriteToken(token);

            _logger.LogInformation("Generated JWT session token for user {UserId}, SessionId: {SessionId}",
                session.UserId, session.SessionId);

            return tokenString;
        }

        /// <summary>
        /// Generate temporary JWT token for OTP verification (valid for 10 minutes)
        /// Replaces hardcoded key in AuthService
        /// </summary>
        public string GenerateTempOtpToken(int userId)
        {
            var claims = new List<Claim>
            {
                new Claim("userId", userId.ToString()),
                new Claim("temp", "true"),
                new Claim("purpose", "otp_verification"),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.SecretKey));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddMinutes(10),
                Issuer = _jwtSettings.Issuer,
                Audience = _jwtSettings.Audience,
                SigningCredentials = credentials
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            var token = tokenHandler.CreateToken(tokenDescriptor);

            _logger.LogInformation("Generated temporary OTP token for user {UserId}", userId);

            return tokenHandler.WriteToken(token);
        }

        /// <summary>
        /// Validate JWT token and extract session ID
        /// Returns session ID (jti claim) if valid, null otherwise
        /// </summary>
        public string? ValidateTokenAndGetSessionId(string token)
        {
            if (string.IsNullOrEmpty(token))
                return null;

            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                
                var principal = tokenHandler.ValidateToken(
                    token, 
                    _tokenValidationParameters, 
                    out SecurityToken validatedToken);

                // Verify token is JWT with correct algorithm
                if (validatedToken is not JwtSecurityToken jwtToken ||
                    !jwtToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, 
                        StringComparison.InvariantCultureIgnoreCase))
                {
                    _logger.LogWarning("Invalid JWT token algorithm");
                    return null;
                }

                // Extract session ID from jti claim
                var sessionId = principal.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
                
                if (string.IsNullOrEmpty(sessionId))
                {
                    _logger.LogWarning("JWT token missing session ID (jti claim)");
                    return null;
                }

                return sessionId;
            }
            catch (SecurityTokenExpiredException)
            {
                _logger.LogWarning("JWT token has expired");
                return null;
            }
            catch (SecurityTokenInvalidSignatureException)
            {
                _logger.LogWarning("JWT token has invalid signature - possible tampering attempt");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "JWT token validation failed");
                return null;
            }
        }

        /// <summary>
        /// Check if token is a temporary OTP token
        /// </summary>
        public bool IsTempOtpToken(string token)
        {
            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                var principal = tokenHandler.ValidateToken(token, _tokenValidationParameters, out _);
                return principal?.FindFirst("temp")?.Value == "true";
            }
            catch
            {
                return false;
            }
        }
    }
}