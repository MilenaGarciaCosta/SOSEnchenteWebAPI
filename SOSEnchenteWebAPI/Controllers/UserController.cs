using Microsoft.AspNetCore.Mvc;
using SOSEnchenteWebAPI.Domain.DTOs;
using SOSEnchenteWebAPI.Domain.Interfaces;
using SOSEnchenteWebAPI.Domain.Models;
using SOSEnchenteWebAPI.Services;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

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
                    IsAdmin = user.IsAdmin,
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

    [Authorize]
    [HttpGet]
    public async Task<ActionResult<UserDto>> GetUser()
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var user = await _userRepository.GetUserByIdAsync(userId);

        if (user == null)
        {
            return NotFound();
        }

        return Ok(ToUserDto(user));
    }

    private static UserDto ToUserDto(User user)
    {
        return new UserDto
        {
            Id = user.Id,
            Email = user.Email,
            Name = user.Name,
            Age = user.Age,
            PhoneNumber = user.PhoneNumber,
            IsAdmin = user.IsAdmin,
            HealthProfile = user.HealthProfile != null ? new HealthProfileDto
            {
                Id = user.HealthProfile.Id,
                MobilityLevel = user.HealthProfile.MobilityLevel,
                LivesAlone = user.HealthProfile.LivesAlone,
                HealthConditions = user.HealthProfile.HealthConditions
            } : null
        };
    }

    private bool TryGetUserId(out int userId)
    {
        var userIdClaim = HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(userIdClaim, out userId);
    }

    [Authorize]
    [HttpPut]
    public async Task<ActionResult<UserDto>> UpdateUser([FromBody] UpdateUserRequest request)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new AuthResponse
            {
                Success = false,
                Message = "Token inválido."
            });
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new AuthResponse
            {
                Success = false,
                Message = "O nome é obrigatório."
            });
        }

        if (request.Age < 1 || request.Age > 120)
        {
            return BadRequest(new AuthResponse
            {
                Success = false,
                Message = "Informe uma idade válida."
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

        if (request.HealthProfile != null && (request.HealthProfile.MobilityLevel < 1 || request.HealthProfile.MobilityLevel > 4))
        {
            return BadRequest(new AuthResponse
            {
                Success = false,
                Message = "Nível de mobilidade inválido."
            });
        }

        var user = await _userRepository.GetUserByIdAsync(userId);

        if (user == null)
        {
            return NotFound(new AuthResponse
            {
                Success = false,
                Message = "Usuário não encontrado."
            });
        }

        user.Name = request.Name.Trim();
        user.Age = request.Age;
        user.PhoneNumber = request.PhoneNumber.Trim();

        if (request.HealthProfile != null)
        {
            user.HealthProfile ??= new HealthProfile();
            user.HealthProfile.MobilityLevel = request.HealthProfile.MobilityLevel;
            user.HealthProfile.LivesAlone = request.HealthProfile.LivesAlone;
            user.HealthProfile.HealthConditions = request.HealthProfile.HealthConditions;
        }

        try
        {
            await _userRepository.SaveChangesAsync();
            return Ok(ToUserDto(user));
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new AuthResponse
            {
                Success = false,
                Message = $"Um erro ocorreu ao atualizar o usuário: {ex.Message}"
            });
        }
    }

    [Authorize]
    [HttpGet("contacts")]
    public async Task<ActionResult<List<EmergencyContactDto>>> GetEmergencyContacts()
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        try
        {
            var emergencyContacts = await _userRepository.GetUserEmergencyContactsAsync(userId);
            var emergencyContactDtos = emergencyContacts.Select(ec => new EmergencyContactDto
            {
                Id = ec.Id,
                Name = ec.Name,
                Description = ec.Description,
                PhoneNumber = ec.PhoneNumber
            }).ToList();

            return Ok(emergencyContactDtos);
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new AuthResponse
            {
                Success = false,
                Message = $"Um erro ocorreu ao buscar por contatos de emergência: {ex.Message}"
            });
        }
    }

    [Authorize]
    [HttpPost("contacts")]
    public async Task<ActionResult<EmergencyContactDto>> AddEmergencyContact([FromBody] CreateEmergencyContactRequest request)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new AuthResponse
            {
                Success = false,
                Message = "O nome é obrigatório."
            });
        }

        if (string.IsNullOrWhiteSpace(request.PhoneNumber))
        {
            return BadRequest(new AuthResponse
            {
                Success = false,
                Message = "O número de telefone é obrigatório."
            });
        }

        if (!_validationService.IsValidPhoneNumber(request.PhoneNumber))
        {
            return BadRequest(new AuthResponse
            {
                Success = false,
                Message = _validationService.GetPhoneNumberValidationErrorMessage()
            });
        }

        try
        {
            var emergencyContact = new EmergencyContact
            {
                UserId = userId,
                Name = request.Name.Trim(),
                Description = request.Description?.Trim() ?? string.Empty,
                PhoneNumber = request.PhoneNumber.Trim()
            };

            var createdContact = await _userRepository.AddEmergencyContactAsync(emergencyContact);

            var emergencyContactDto = new EmergencyContactDto
            {
                Id = createdContact.Id,
                Name = createdContact.Name,
                Description = createdContact.Description,
                PhoneNumber = createdContact.PhoneNumber
            };

            return CreatedAtAction(nameof(AddEmergencyContact), new { id = createdContact.Id }, emergencyContactDto);
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new AuthResponse
            {
                Success = false,
                Message = $"Um erro ocorreu ao adicionar o contato de emergência: {ex.Message}"
            });
        }
    }

    [Authorize]
    [HttpGet("dependents")]
    public async Task<ActionResult<List<DependentDto>>> GetDependents()
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId) || !int.TryParse(userId, out var parsedUserId))
            {
                return Unauthorized(new AuthResponse
                {
                    Success = false,
                    Message = "Usuário não autenticado."
                });
            }

            var dependents = await _userRepository.GetDependentsAsync(parsedUserId);
            var dependentsDto = dependents.Select(d => new DependentDto
            {
                Id = d.Id,
                Name = d.Name,
                Age = d.Age
            }).ToList();

            return Ok(dependentsDto);
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new AuthResponse
            {
                Success = false,
                Message = $"Um erro ocorreu ao buscar os dependentes: {ex.Message}"
            });
        }
    }

    [Authorize]
    [HttpPost("dependents")]
    public async Task<ActionResult<DependentDto>> AddDependent([FromBody] AddDependentRequest request)
    {
        try
        {
            var userEmail = User.FindFirst(ClaimTypes.Email)?.Value;
            if (string.IsNullOrEmpty(userEmail))
            {
                return Unauthorized(new AuthResponse
                {
                    Success = false,
                    Message = "Usuário não autenticado."
                });
            }

            if (request.DependentEmail == null)
            {
                return BadRequest(new AuthResponse
                {
                    Success = false,
                    Message = "O email do dependente deve ser válido."
                });
            }

            if (userEmail == request.DependentEmail)
            {
                return BadRequest(new AuthResponse
                {
                    Success = false,
                    Message = "Um usuário não pode ser seu próprio dependente."
                });
            }

            var dependentUser = await _userRepository.GetUserByEmailAsync(request.DependentEmail);
            if (dependentUser == null)
            {
                return NotFound(new AuthResponse
                {
                    Success = false,
                    Message = "Dependente não encontrado."
                });
            }

            var requesterUser = await _userRepository.GetUserByEmailAsync(userEmail);

            var dependent = new Dependent
            {
                UserId = requesterUser.Id,
                DependentId = dependentUser.Id
            };

            var createdDependent = await _userRepository.AddDependentAsync(dependent);

            var dependentDto = new DependentDto
            {
                Id = createdDependent.DependentId,
                Name = dependentUser.Name,
                Age = dependentUser.Age
            };

            return CreatedAtAction(nameof(AddDependent), new { id = createdDependent.DependentId }, dependentDto);
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new AuthResponse
            {
                Success = false,
                Message = $"Um erro ocorreu ao adicionar o dependente: {ex.Message}"
            });
        }
    }
}
