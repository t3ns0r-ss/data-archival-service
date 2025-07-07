using System.Security.Claims;
using DataArchival.Core.Interfaces;
using DataArchival.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DataArchival.App.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ArchiveController : ControllerBase
{
    private readonly IArchiveDataService _archiveDataService;
    private readonly IDataArchivalService _dataArchivalService;
    private readonly IAuthenticationService _authService;

    public ArchiveController(
        IArchiveDataService archiveDataService,
        IDataArchivalService dataArchivalService,
        IAuthenticationService authService)
    {
        _archiveDataService = archiveDataService;
        _dataArchivalService = dataArchivalService;
        _authService = authService;
    }

    [HttpGet("{tableName}")]
    public async Task<ActionResult<ApiResponse<List<Dictionary<string, object>>>>> GetArchivedData(
        string tableName,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 100)
    {
        var username = User.FindFirst(ClaimTypes.Name)?.Value;
        if (username == null || !await _authService.HasTableAccessAsync(username, tableName))
        {
            return Forbid();
        }

        var data = await _archiveDataService.GetArchivedDataAsync(tableName, page, pageSize);
        return Ok(new ApiResponse<List<Dictionary<string, object>>>
        {
            IsSuccess = true,
            Data = data
        });
    }

    [HttpGet("{tableName}/count")]
    public async Task<ActionResult<ApiResponse<int>>> GetArchivedDataCount(string tableName)
    {
        var username = User.FindFirst(ClaimTypes.Name)?.Value;
        if (username == null || !await _authService.HasTableAccessAsync(username, tableName))
        {
            return Forbid();
        }

        var count = await _archiveDataService.GetArchivedDataCountAsync(tableName);
        return Ok(new ApiResponse<int>
        {
            IsSuccess = true,
            Data = count
        });
    }

    [HttpPost("run/{tableName}")]
    [Authorize(Roles = "admin")]
    public async Task<ActionResult<ApiResponse<int>>> RunArchival(string tableName)
    {
        var archivedCount = await _dataArchivalService.ArchiveTableDataAsync(tableName);
        return Ok(new ApiResponse<ArchiveLog>
        {
            IsSuccess = true,
            Message = $"Archived {archivedCount} records",
            Data = archivedCount
        });
    }

    [HttpPost("run-all")]
    [Authorize(Roles = "admin")]
    public async Task<ActionResult<ApiResponse<bool>>> RunAllArchival()
    {
        var success = await _dataArchivalService.ArchiveAllConfiguredTablesAsync();
        return Ok(new ApiResponse<IEnumerable<ArchiveLog>>
        {
            IsSuccess = success.Any(),
            Message = success.Any() ? "All archival processes completed" : "Some archival processes failed",
            Data = success
        });
    }
}