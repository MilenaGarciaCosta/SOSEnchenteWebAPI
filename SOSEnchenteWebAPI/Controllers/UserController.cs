using Microsoft.AspNetCore.Mvc;
using SOSEnchenteWebAPI.Domain.DTOs;
using SOSEnchenteWebAPI.Domain.Interfaces;
using SOSEnchenteWebAPI.Domain.Models;
using SOSEnchenteWebAPI.Services;

namespace SOSEnchenteWebAPI.Controllers;

[Route("api/user")]
[ApiController]
public class UserController : ControllerBase
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordService _passwordService;
    private readonly IJwtService _jwtService;
    private readonly IValidationService _validationService;

    public UserController(IUserRepository userRepository, IPasswordService passwordService, IJwtService jwtService, IValidationService validationService)
    {
        _userRepository = userRepository;
        _passwordService = passwordService;
        _jwtService = jwtService;
        _validationService = validationService;
    }

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register([FromBody] RegisterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password) || string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new AuthResponse
            {
                Success = false,
                Message = "Email, senha, e nome são obrigatórios."
            });
        }

        if (!_validationService.IsValidEmail(request.Email))
        {
            return BadRequest(new AuthResponse
            {
                Success = false,
                Message = _validationService.GetEmailValidationErrorMessage()
            });
        }

        if (!string.IsNullOrWhiteSpace(request.PhoneNumber) && !_validationService.IsValidPhoneNumber(request.PhoneNumber))
        {
            return BadRequest(new AuthResponse
            {
                Success = false,
                Message = _validationService.GetPhoneNumberValidationErrorMessage()
            });
        }

        if (await _userRepository.UserExistsAsync(request.Email))
        {
            return BadRequest(new AuthResponse
            {
                Success = false,
                Message = "Email já registrado."
            });
        }

        var hashedPassword = _passwordService.HashPassword(request.Password);

        var healthProfile = new HealthProfile
        {
            MobilityLevel = request.HealthProfile.MobilityLevel,
            LivesAlone = request.HealthProfile.LivesAlone,
            HealthConditions = request.HealthProfile.HealthConditions
        };

        var user = new User
        {
            Email = request.Email,
            Password = hashedPassword,
            Name = request.Name,
            Age = request.Age,
            PhoneNumber = request.PhoneNumber,
            HealthProfile = healthProfile
        };

        try
        {
            var createdUser = await _userRepository.CreateUserAsync(user);
            var token = _jwtService.GenerateToken(createdUser);

            return CreatedAtAction(nameof(Register), new AuthResponse
            {
                Success = true,
                Message = "Registrado com sucesso.",
                Token = token,
                User = new UserDto
                {
                    Id = createdUser.Id,
                    Email = createdUser.Email,
                    Name = createdUser.Name,
                    Age = createdUser.Age,
                    PhoneNumber = createdUser.PhoneNumber,
                    HealthProfile = new HealthProfileDto
                    {
                        Id = createdUser.HealthProfile.Id,
                        MobilityLevel = createdUser.HealthProfile.MobilityLevel,
                        LivesAlone = createdUser.HealthProfile.LivesAlone,
                        HealthConditions = createdUser.HealthProfile.HealthConditions
                    }
                }
            });
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new AuthResponse
            {
                Success = false,
                Message = $"Um erro ocorreu durante o registro: {ex.Message}"
            });
        }
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new AuthResponse
            {
                Success = false,
                Message = "Email e senha são obrigatórios."
            });
        }

        if (!_validationService.IsValidEmail(request.Email))
        {
            return BadRequest(new AuthResponse
            {
                Success = false,
                Message = _validationService.GetEmailValidationErrorMessage()
            });
        }

        try
        {
            var user = await _userRepository.GetUserByEmailAsync(request.Email);

            if (user == null)
            {
                return Unauthorized(new AuthResponse
                {
                    Success = false,
                    Message = "Email não encontrado."
                });
            }

            if (!_passwordService.VerifyPassword(request.Password, user.Password))
            {
                return Unauthorized(new AuthResponse
                {
                    Success = false,
                    Message = "Senha incorreta."
                });
            }

            var token = _jwtService.GenerateToken(user);

            return Ok(new AuthResponse
            {
                Success = true,
                Message = "Sucesso ao fazer login.",
                Token = token,
                User = new UserDto
                {
                    Id = user.Id,
                    Email = user.Email,
                    Name = user.Name,
                    Age = user.Age,
                    PhoneNumber = user.PhoneNumber,
                    HealthProfile = user.HealthProfile != null ? new HealthProfileDto
                    {
                        Id = user.HealthProfile.Id,
                        MobilityLevel = user.HealthProfile.MobilityLevel,
                        LivesAlone = user.HealthProfile.LivesAlone,
                        HealthConditions = user.HealthProfile.HealthConditions
                    } : null
                }
            });
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new AuthResponse
            {
                Success = false,
                Message = $"Um erro ocorreu durante o login: {ex.Message}"
            });
        }
    }
}
