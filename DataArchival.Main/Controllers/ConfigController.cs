using DataArchival.Core.Interfaces;
using DataArchival.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DataArchival.Main.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ConfigController : ControllerBase
{
    private readonly IArchiveConfigService _configService;

    public ConfigController(IArchiveConfigService configService)
    {
        _configService = configService;
    }

    [HttpGet("get-configs")]
    [Authorize(Roles = "admin")]
    public async Task<ActionResult<ApiResponse<List<ArchiveConfig>>>> GetConfigs()
    {
        var configs = await _configService.GetAllConfigsAsync();
        return Ok(new ApiResponse<List<ArchiveConfig>>
        {
            IsSuccess = true,
            Data = configs
        });
    }

    [HttpGet("get-config/{tableName}")]
    [Authorize(Roles = "admin")]
    public async Task<ActionResult<ApiResponse<ArchiveConfig>>> GetConfig(string tableName)
    {
        var config = await _configService.GetConfigAsync(tableName);
        if (config == null)
        {
            return NotFound(new ApiResponse<ArchiveConfig>
            {
                IsSuccess = false,
                Message = "Configuration not found"
            });
        }

        return Ok(new ApiResponse<ArchiveConfig>
        {
            IsSuccess = true,
            Data = config
        });
    }

    [HttpPost("create-config")]
    [Authorize(Roles = "admin")]
    public async Task<ActionResult<ApiResponse<ArchiveConfig>>> CreateConfig([FromBody] ArchiveConfig config)
    {
        var created = await _configService.CreateConfigAsync(config);
        return CreatedAtAction(nameof(GetConfig), new { tableName = created.TableName },
            new ApiResponse<ArchiveConfig>
            {
                IsSuccess = true,
                Message = "Configuration created successfully",
                Data = created
            });
    }

    [HttpPut("update-config/{tableName}")]
    [Authorize(Roles = "admin")]
    public async Task<ActionResult<ApiResponse<ArchiveConfig>>> UpdateConfig(string tableName,
        [FromBody] ArchiveConfig config)
    {
        config.TableName = tableName;
        var updated = await _configService.UpdateConfigAsync(config);
        return Ok(new ApiResponse<ArchiveConfig>
        {
            IsSuccess = true,
            Message = "Configuration updated successfully",
            Data = updated
        });
    }

    [HttpDelete("delete-config/{tableName}")]
    [Authorize(Roles = "admin")]
    public async Task<ActionResult<ApiResponse<bool>>> DeleteConfig(string tableName)
    {
        var deleted = await _configService.DeleteConfigAsync(tableName);
        if (!deleted)
        {
            return NotFound(new ApiResponse<bool>
            {
                IsSuccess = false,
                Message = "Configuration not found"
            });
        }

        return Ok(new ApiResponse<bool>
        {
            IsSuccess = true,
            Message = "Configuration deleted successfully",
            Data = true
        });
    }
}