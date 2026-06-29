using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using RealTimeDashboard.Application.DTOs;
using RealTimeDashboard.Application.Interfaces;
using RealTimeDashboard.Domain.Entities;
using RealTimeDashboard.Domain.Exceptions;

namespace RealTimeDashboard.Infrastructure.Auth;

public class AuthService : IAuthService
{
    public const string AdminRole = "Admin";
    public const string CustomerRole = "Customer";

    private readonly UserManager<AppUser> _userManager;
    private readonly TokenService _tokenService;

    public AuthService(UserManager<AppUser> userManager, TokenService tokenService)
    {
        _userManager = userManager;
        _tokenService = tokenService;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        var existing = await _userManager.FindByEmailAsync(request.Email);
        if (existing is not null)
            throw new BusinessRuleException("A user with this email already exists.");

        var user = new AppUser
        {
            UserName = request.Email,
            Email = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName,
            CreatedAt = DateTime.UtcNow
        };

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            var errors = result.Errors.ToDictionary(e => e.Code, e => new[] { e.Description });
            throw new ValidationException(errors);
        }

        await _userManager.AddToRoleAsync(user, CustomerRole);

        return await BuildResponseAsync(user);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is null || !await _userManager.CheckPasswordAsync(user, request.Password))
            throw new BusinessRuleException("Invalid email or password.");

        return await BuildResponseAsync(user);
    }

    public async Task<AuthResponse> RefreshAsync(RefreshTokenRequest request, CancellationToken ct = default)
    {
        var principal = _tokenService.GetPrincipalFromExpiredToken(request.AccessToken)
            ?? throw new BusinessRuleException("Invalid access token.");

        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            throw new BusinessRuleException("Invalid access token.");

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null ||
            user.RefreshToken != request.RefreshToken ||
            user.RefreshTokenExpiry is null ||
            user.RefreshTokenExpiry <= DateTime.UtcNow)
        {
            throw new BusinessRuleException("Invalid or expired refresh token.");
        }

        return await BuildResponseAsync(user);
    }

    private async Task<AuthResponse> BuildResponseAsync(AppUser user)
    {
        var roles = await _userManager.GetRolesAsync(user);
        var accessToken = _tokenService.CreateAccessToken(user, roles);
        var refreshToken = _tokenService.CreateRefreshToken();

        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiry = _tokenService.RefreshTokenExpiry;
        await _userManager.UpdateAsync(user);

        return new AuthResponse(
            accessToken,
            refreshToken,
            _tokenService.AccessTokenExpiry,
            user.Email ?? string.Empty,
            user.FullName,
            roles);
    }
}
