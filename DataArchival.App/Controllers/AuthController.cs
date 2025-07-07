using System.Net;
using DataArchival.Core.Interfaces;
using DataArchival.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace DataArchival.App.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthenticationService _authService;

    public AuthController(IAuthenticationService authService)
    {
        _authService = authService;
    }

    [HttpPost("login")]
    public async Task<ActionResult<ApiResponse<LoginResponse>>> Login([FromBody] LoginRequest request)
    {
        var response = await _authService.AuthenticateAsync(request);
        if (response == null)
        {
            return Unauthorized(new ApiResponse<LoginResponse>
            {
                IsSuccess = false,
                Message = "Invalid credentials"
            });
        }

        return Ok(new ApiResponse<LoginResponse>
        {
            IsSuccess = true,
            Message = "Authentication successful",
            Data = response
        });
    }
    
    [HttpPost("register")]
    public async Task<ActionResult<ApiResponse<LoginResponse>>> Register([FromBody] LoginRequest request)
    {
        var response = await _authService.RegisterUserAsync(request);
        if (!response)
        {
            return StatusCode((int)HttpStatusCode.InternalServerError, new ApiResponse<LoginResponse>
            {
                IsSuccess = false,
                Message = "User creation failed"
            });
        }

        return Ok(new ApiResponse<LoginResponse>
        {
            IsSuccess = true,
            Message = "User creation successful",
            Data = null
        });
    }
    [HttpPost("refresh-token")]
    public async Task<ActionResult<ApiResponse<LoginResponse>>> RefreshToken()
    {
        var bearerToken = Request.Headers["Authorization"].FirstOrDefault()?.Replace("Bearer ", "");
        if (string.IsNullOrEmpty(bearerToken))
        {
            return Unauthorized(new ApiResponse<LoginResponse>
            {
                IsSuccess = false,
                Message = "Bearer token is missing"
            });
        }

        var response = await _authService.RefreshToken(bearerToken);
        if (response is null)
        {
            return Unauthorized(new ApiResponse<LoginResponse>
            {
                IsSuccess = false,
                Message = "Invalid or expired refresh token"
            });
        }

        return Ok(new ApiResponse<LoginResponse>
        {
            IsSuccess = true,
            Message = "Token refreshed successfully",
            Data = response
        });
    }
}