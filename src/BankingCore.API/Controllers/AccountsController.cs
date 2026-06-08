using BankingCore.API.Extensions;
using BankingCore.Application.DTOs;
using BankingCore.Application.UseCases;
using BankingCore.Domain.Enums;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BankingCore.API.Controllers;

/// <summary>
/// Controller de contas bancárias (RF02).
/// CRUD completo: Create, Read, Update status, Soft Delete.
/// Authorization: usuário autenticado só acessa sua própria conta (CONSTITUTION Lei I.4 — Previne IDOR).
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AccountsController : ControllerBase
{
    private readonly CreateAccountUseCase _createAccountUseCase;
    private readonly GetAccountUseCase _getAccountUseCase;
    private readonly GetAllAccountsUseCase _getAllAccountsUseCase;
    private readonly UpdateAccountStatusUseCase _updateAccountStatusUseCase;
    private readonly AddBalanceUseCase _addBalanceUseCase;
    private readonly GetStatementUseCase _getStatementUseCase;
    private readonly IValidator<CreateAccountRequest> _createValidator;
    private readonly IValidator<UpdateAccountStatusRequest> _updateStatusValidator;
    private readonly IValidator<StatementRequest> _statementValidator;

    public AccountsController(
        CreateAccountUseCase createAccountUseCase,
        GetAccountUseCase getAccountUseCase,
        GetAllAccountsUseCase getAllAccountsUseCase,
        UpdateAccountStatusUseCase updateAccountStatusUseCase,
        AddBalanceUseCase addBalanceUseCase,
        GetStatementUseCase getStatementUseCase,
        IValidator<CreateAccountRequest> createValidator,
        IValidator<UpdateAccountStatusRequest> updateStatusValidator,
        IValidator<StatementRequest> statementValidator)
    {
        _createAccountUseCase = createAccountUseCase;
        _getAccountUseCase = getAccountUseCase;
        _getAllAccountsUseCase = getAllAccountsUseCase;
        _updateAccountStatusUseCase = updateAccountStatusUseCase;
        _addBalanceUseCase = addBalanceUseCase;
        _getStatementUseCase = getStatementUseCase;
        _createValidator = createValidator;
        _updateStatusValidator = updateStatusValidator;
        _statementValidator = statementValidator;
    }

    /// <summary>
    /// Cria uma nova conta bancária vinculada ao usuário autenticado.
    /// Apenas uma conta ativa por usuário é permitida (1:1).
    /// </summary>
    /// <remarks>
    /// Exemplo de request:
    ///
    ///     POST /api/accounts
    ///     {
    ///       "holderName": "João Silva",
    ///       "holderEmail": "joao@exemplo.com",
    ///       "holderCpf": "12345678901"
    ///     }
    /// </remarks>
    [HttpPost]
    [ProducesResponseType(typeof(AccountResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create([FromBody] CreateAccountRequest request, CancellationToken ct)
    {
        var validation = await _createValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return ValidationProblem(validation.Errors.Select(e => e.ErrorMessage).ToArray());

        var userId = HttpContext.GetUserId();
        var response = await _createAccountUseCase.ExecuteAsync(userId, request, ct);

        return CreatedAtAction(nameof(Get), new { id = response.AccountId }, response);
    }

    /// <summary>
    /// Retorna os dados de uma conta. Usuário só pode acessar sua própria conta.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(AccountResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get([FromRoute] Guid id, CancellationToken ct)
    {
        var userId = HttpContext.GetUserId();
        var response = await _getAccountUseCase.ExecuteAsync(id, ct);

        // CONSTITUTION Lei I.4: Autorização explícita — só dono acessa sua conta
        if (response.UserId is null || response.UserId != userId)
            return Forbid();

        return Ok(response);
    }

    /// <summary>
    /// Lista todas as contas do sistema. Endpoint para dev/admin.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<AccountResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var accounts = await _getAllAccountsUseCase.ExecuteAsync(ct);
        return Ok(accounts);
    }

    /// <summary>
    /// Adiciona saldo à conta. Endpoint para dev/test — carrega saldo inicial.
    /// </summary>
    /// <remarks>
    /// Exemplo de request:
    ///
    ///     POST /api/accounts/{id}/balance
    ///     {
    ///       "amount": 5000.00
    ///     }
    /// </remarks>
    [HttpPost("{id:guid}/balance")]
    [ProducesResponseType(typeof(AccountResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddBalance(
        [FromRoute] Guid id,
        [FromBody] AddBalanceRequest request,
        CancellationToken ct)
    {
        if (request.Amount <= 0)
            return BadRequest(new ProblemDetails
            {
                Status = 400,
                Title = "Valor inválido",
                Detail = "O valor deve ser maior que zero.",
                Instance = HttpContext.Request.Path
            });

        var response = await _addBalanceUseCase.ExecuteAsync(id, request.Amount, ct);
        return Ok(response);
    }

    /// <summary>
    /// Atualiza o status de uma conta (Active ↔ Blocked).
    /// Para encerrar a conta, use DELETE.
    /// </summary>
    /// <remarks>
    /// Exemplo de request:
    ///
    ///     PUT /api/accounts/{id}/status
    ///     {
    ///       "status": "Blocked"
    ///     }
    /// </remarks>
    [HttpPut("{id:guid}/status")]
    [ProducesResponseType(typeof(AccountResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateStatus(
        [FromRoute] Guid id,
        [FromBody] UpdateAccountStatusRequest request,
        CancellationToken ct)
    {
        var validation = await _updateStatusValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return ValidationProblem(validation.Errors.Select(e => e.ErrorMessage).ToArray());

        var userId = HttpContext.GetUserId();

        // Verifica ownership antes de atualizar (previne IDOR — CONSTITUTION Lei I.4)
        var existing = await _getAccountUseCase.ExecuteAsync(id, ct);
        if (existing.UserId is null || existing.UserId != userId)
            return Forbid();

        var response = await _updateAccountStatusUseCase.ExecuteAsync(id, request.Status, ct);
        return Ok(response);
    }

    /// <summary>
    /// Encerra a conta (soft delete — altera status para Closed).
    /// Apenas o dono pode encerrar a conta.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete([FromRoute] Guid id, CancellationToken ct)
    {
        var userId = HttpContext.GetUserId();

        // Verifica ownership antes de deletar
        var existing = await _getAccountUseCase.ExecuteAsync(id, ct);
        if (existing.UserId is null || existing.UserId != userId)
            return Forbid();

        // Status Closed é aplicado via UpdateStatus com AccountStatus.Closed
        await _updateAccountStatusUseCase.ExecuteAsync(id, AccountStatus.Closed, ct);

        return NoContent();
    }

    /// <summary>
    /// Consulta o extrato bancário com paginação por cursor (keyset).
    /// </summary>
    /// <remarks>
    /// **Paginação por cursor:**
    /// Use o `nextCursor` da resposta anterior para obter a próxima página.
    /// Na primeira chamada, não envie `cursor` (será null).
    ///
    /// **RN04 - Transparência PIX:**
    /// Todos os responses incluem o `EndToEndId` (E2E ID) para auditoria.
    /// Dados do recebedor (nome, CPF) vêm mascarados diretamente do MySQL.
    ///
    /// Exemplo de request:
    ///
    ///     GET /api/accounts/{id}/statement?limit=20&amp;startDate=2024-01-01&amp;endDate=2024-12-31
    ///     GET /api/accounts/{id}/statement?limit=20&amp;cursor=150
    ///
    /// Resposta (200):
    ///
    ///     {
    ///       "transactions": [
    ///         {
    ///           "transactionId": 150,
    ///           "endToEndId": "E123456789012...",
    ///           "date": "2024-06-01T14:30:00Z",
    ///           "type": "Debit",
    ///           "description": null,
    ///           "amount": 100.50,
    ///           "status": "COMPLETED",
    ///           "counterpartyName": "Jo***",
    ///           "counterpartyDocument": "550e8400-e29b-41d4-a716-446655440002"
    ///         }
    ///       ],
    ///       "nextCursor": "149",
    ///       "hasMore": true
    ///     }
    /// </remarks>
    [HttpGet("{id:guid}/statement")]
    [ProducesResponseType(typeof(StatementResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStatement(
        [FromRoute] Guid id,
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate,
        [FromQuery] string? cursor,
        [FromQuery] int? limit,
        CancellationToken ct)
    {
        var userId = HttpContext.GetUserId();

        // Verifica ownership — só dono vê seu extrato (CONSTITUTION Lei I.4)
        var account = await _getAccountUseCase.ExecuteAsync(id, ct);
        if (account.UserId is null || account.UserId != userId)
            return Forbid();

        var request = new StatementRequest(
            AccountId: id,
            StartDate: startDate,
            EndDate: endDate,
            Cursor: cursor,
            Limit: limit);

        var validation = await _statementValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return ValidationProblem(validation.Errors.Select(e => e.ErrorMessage).ToArray());

        var response = await _getStatementUseCase.ExecuteAsync(request, ct);
        return Ok(response);
    }

    private IActionResult ValidationProblem(string[] errors)
    {
        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status422UnprocessableEntity,
            Title = "Erros de validação",
            Type = "https://httpstatuses.com/422",
            Instance = HttpContext.Request.Path
        };
        problemDetails.Extensions["errors"] = errors;
        return new ObjectResult(problemDetails) { StatusCode = StatusCodes.Status422UnprocessableEntity };
    }
}
