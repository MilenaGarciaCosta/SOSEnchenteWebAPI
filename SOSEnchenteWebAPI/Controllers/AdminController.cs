using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SOSEnchenteWebAPI.Domain.DTOs;
using SOSEnchenteWebAPI.Domain.Interfaces;
using SOSEnchenteWebAPI.Services;

namespace SOSEnchenteWebAPI.Controllers;

[Route("api/admin")]
[ApiController]
[Authorize(Roles = "Admin")]
public class AdminController : ControllerBase
{
    private readonly IUserRepository _userRepository;
    private readonly IPriorityService _priorityService;

    public AdminController(IUserRepository userRepository, IPriorityService priorityService)
    {
        _userRepository = userRepository;
        _priorityService = priorityService;
    }

    [HttpGet("users")]
    public async Task<ActionResult<List<RescueSummaryDto>>> GetRescueQueue()
    {
        var users = await _userRepository.GetAllUsersAsync();

        var queue = users
            .Select(_priorityService.CalculateRescueSummary)
            .OrderByDescending(summary => summary.Score)
            .ThenByDescending(summary => summary.Age)
            .ToList();

        return Ok(queue);
    }
}