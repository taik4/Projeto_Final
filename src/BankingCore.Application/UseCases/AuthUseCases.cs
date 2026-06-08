using BankingCore.Application.DTOs;
using BankingCore.Application.Interfaces;
using BankingCore.Application.Services;
using BankingCore.Domain.Entities;
using BankingCore.Domain.Exceptions;
using BankingCore.Domain.Interfaces;
using BankingCore.Domain.Utils;

namespace BankingCore.Application.UseCases;

/// <summary>
/// Use Case: Registrar e autenticar usuários.
/// Controllers magros — toda a lógica vive aqui (CONSTITUTION Lei III.4).
/// </summary>
public class AuthUseCases
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly JwtService _jwtService;

    public AuthUseCases(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        JwtService jwtService)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _jwtService = jwtService;
    }

    /// <summary>
    /// Registra um novo usuário no sistema.
    /// CPF é hasheado com SHA-256 e senha com BCrypt (nunca armazena PII pleno).
    /// </summary>
    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        // Valida se email já existe
        if (await _userRepository.ExistsByEmailAsync(request.Email.Trim().ToLower(), ct))
            throw new DomainException("Email já cadastrado no sistema.");

        // Hash do CPF via SHA-256 (CONSTITUTION Lei I.2)
        var cpfDigits = new string(request.Cpf.Where(char.IsDigit).ToArray());
        var cpfHash = Sha256Helper.Compute(cpfDigits);

        // Valida CPF não duplicado via hash
        if (await _userRepository.GetByCpfHashAsync(cpfHash, ct) is not null)
            throw new DomainException("CPF já cadastrado no sistema.");

        // Hash da senha via BCrypt
        var passwordHash = _passwordHasher.HashPassword(request.Password);

        // Cria entidade User
        var user = new User(
            email: request.Email.Trim().ToLower(),
            passwordHash: passwordHash,
            cpfHash: cpfHash
        );

        await _userRepository.AddAsync(user, ct);

        // Gera tokens JWT
        return BuildAuthResponse(user);
    }

    /// <summary>
    /// Autentica o usuário por email + senha e retorna JWT.
    /// </summary>
    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var email = request.Email.Trim().ToLower();

        // Busca usuário pelo email
        var user = await _userRepository.GetByEmailAsync(email, ct)
            ?? throw new UnauthorizedException("Email ou senha inválidos.");

        // Verifica senha
        if (!_passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
            throw new UnauthorizedException("Email ou senha inválidos.");

        // Gera tokens JWT
        return BuildAuthResponse(user);
    }

    private AuthResponse BuildAuthResponse(User user)
    {
        var accessToken = _jwtService.GenerateAccessToken(user);
        var refreshToken = _jwtService.GenerateRefreshToken();
        var expiresAt = DateTime.UtcNow.AddMinutes(15); // Match JwtSettings.ExpirationMinutes

        return new AuthResponse(accessToken, refreshToken, expiresAt);
    }
}
