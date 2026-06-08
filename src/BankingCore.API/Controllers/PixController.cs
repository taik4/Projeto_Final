using BankingCore.API.Extensions;
using BankingCore.Application.DTOs;
using BankingCore.Application.UseCases;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BankingCore.API.Controllers;

/// <summary>
/// Controller de operações PIX (RF03).
/// Endpoint principal: POST /api/pix/transfer — executa transferência via SP com idempotência.
/// </summary>
[ApiController]
[Route("api/pix")]
[Authorize]
public class PixController : ControllerBase
{
    private readonly TransferPixUseCase _transferPixUseCase;

    public PixController(TransferPixUseCase transferPixUseCase)
    {
        _transferPixUseCase = transferPixUseCase;
    }

    /// <summary>
    /// Executa uma transferência PIX interna entre duas contas do sistema.
    /// Usa Stored Procedure com idempotência (RN02), atomicidade (RN01)
    /// e lock pessimista NOWAIT (previne deadlocks).
    /// </summary>
    /// <remarks>
    /// **Fluxo:**
    ///
    ///     1. Server valida request (FluentValidation).
    ///     2. Server identifica conta de origem pelo JWT (previne IDOR).
    ///     3. Server gera EndToEndId único (padrão BACEN 32 chars) se não fornecido.
    ///     4. Chama SP sp_process_pix_transfer via Dapper.
    ///     5. Publica evento PixTransferCompletedEvent em caso de sucesso.
    ///
    /// **Idempotência**: Se uma transferência com mesma `IdempotencyKey` já foi processada,
    /// a SP retorna `Status = "DUPLICATE"` com o `EndToEndId` original (sem debitar saldo novamente).
    ///
    /// **Exemplo de request:**
    ///
    ///     POST /api/pix/transfer
    ///     {
    ///       "targetAccountId": "6ba7b810-98ad-4116-a947-2de217cfe384",
    ///       "amount": 150.50,
    ///       "description": "Pagamento almoço",
    ///       "receiverName": "Maria Santos",
    ///       "receiverDocMasked": "603***-39a",
    ///       "idempotencyKey": "550e8400-e29b-41d4-a716-446655440001"
    ///     }
    ///
    /// **Respostas possíveis:**
    /// | Status HTTP | Status PIX | Descrição |
    /// |-------------|------------|-----------|
    /// | 200 OK | SETTLED | Transferência efetivada com sucesso |
    /// | 200 OK | DUPLICATE | Idempotência ativada — transferência já processada |
    /// | 200 OK | REJECTED_* | Negócio rejeitado (saldo insuficiente, conta inativa, etc.) |
    /// | 400 | - | Erro de validação/banco |
    /// | 404 | - | Conta não encontrada |
    /// | 422 | - | Erro de validação (FluentValidation) |
    /// </remarks>
    [HttpPost("transfer")]
    [ProducesResponseType(typeof(PixTransferResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Transfer([FromBody] PixTransferRequest request, CancellationToken ct)
    {
        var userId = HttpContext.GetUserId();
        var response = await _transferPixUseCase.ExecuteAsync(userId, request, ct);

        // Mapeia status do PIX para HTTP status adequado
        var httpStatus = response.Status switch
        {
            "SETTLED" or "DUPLICATE" => StatusCodes.Status200OK,
            _ => StatusCodes.Status200OK // rejeições de negócio também retornam 200 com Status=REJECTED_*
        };

        return StatusCode(httpStatus, response);
    }
}
