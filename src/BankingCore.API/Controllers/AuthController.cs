using BankingCore.Application.DTOs;
using BankingCore.Application.UseCases;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace BankingCore.API.Controllers;

/// <summary>
/// Controller de autenticação.
/// Endpoints: POST /api/auth/register e POST /api/auth/login.
/// Controllers magros (CONSTITUTION Lei III.4): recebe DTO, valida, chama Use Case, retorna response.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AuthUseCases _authUseCases;
    private readonly IValidator<RegisterRequest> _registerValidator;
    private readonly IValidator<LoginRequest> _loginValidator;

    public AuthController(
        AuthUseCases authUseCases,
        IValidator<RegisterRequest> registerValidator,
        IValidator<LoginRequest> loginValidator)
    {
        _authUseCases = authUseCases;
        _registerValidator = registerValidator;
        _loginValidator = loginValidator;
    }

    /// <summary>
    /// Registra um novo usuário. O CPF e a senha são hasheados antes de persistir.
    /// </summary>
    /// <remarks>
    /// Exemplo de request:
    ///
    ///     POST /api/auth/register
    ///     {
    ///       "email": "joao@exemplo.com",
    ///       "password": "MinhaSenha@123",
    ///       "cpf": "12345678901",
    ///       "holderName": "João Silva"
    ///     }
    ///
    /// Response (201 Created):
    ///
    ///     {
    ///       "accessToken": "eyJhbGciOiJSUzI1NiIs...",
    ///       "refreshToken": "base64...",
    ///       "expiresAt": "2024-01-01T12:15:00.000Z"
    ///     }
    /// </remarks>
    [HttpPost("register")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Register(
        [FromBody] RegisterRequest request,
        CancellationToken ct)
    {
        var validation = await _registerValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return ValidationProblem(
                validation.Errors.Select(e => e.ErrorMessage).ToArray(),
                statusCode: StatusCodes.Status422UnprocessableEntity);
        }

        var response = await _authUseCases.RegisterAsync(request, ct);
        return CreatedAtAction(nameof(Login), new { }, response);
    }

    /// <summary>
    /// Autentica o usuário com email e senha, retornando JWT RS256.
    /// </summary>
    /// <remarks>
    /// Exemplo de request:
    ///
    ///     POST /api/auth/login
    ///     {
    ///       "email": "joao@exemplo.com",
    ///       "password": "MinhaSenha@123"
    ///     }
    ///
    /// Response (200 OK):
    ///
    ///     {
    ///       "accessToken": "eyJhbGciOiJSUzI1NiIs...",
    ///       "refreshToken": "base64...",
    ///       "expiresAt": "2024-01-01T12:15:00.000Z"
    ///     }
    /// </remarks>
    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequest request,
        CancellationToken ct)
    {
        var validation = await _loginValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return ValidationProblem(
                validation.Errors.Select(e => e.ErrorMessage).ToArray(),
                statusCode: StatusCodes.Status422UnprocessableEntity);
        }

        var response = await _authUseCases.LoginAsync(request, ct);
        return Ok(response);
    }

    /// <summary>
    /// Helper para retornar ProblemDetails com erros de validação (FluentValidation).
    /// </summary>
    private IActionResult ValidationProblem(string[] errors, int statusCode)
    {
        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = "Erros de validação",
            Type = $"https://httpstatuses.com/{statusCode}",
            Instance = HttpContext.Request.Path
        };
        problemDetails.Extensions["errors"] = errors;
        return new ObjectResult(problemDetails) { StatusCode = statusCode };
    }
}
