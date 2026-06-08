using BankingCore.Application.DTOs;
using BankingCore.Domain.DTOs;
using BankingCore.Domain.Entities;
using BankingCore.Domain.Enums;
using BankingCore.Domain.Exceptions;
using BankingCore.Domain.Interfaces;
using BankingCore.Domain.Utils;

namespace BankingCore.Application.UseCases;

/// <summary>
/// Use Case: Criar nova conta bancária.
/// Valida que o usuário ainda não possui uma conta ativa (1:1).
/// </summary>
public sealed class CreateAccountUseCase
{
    private readonly IAccountRepository _accountRepository;
    private readonly IUserRepository _userRepository;

    public CreateAccountUseCase(IAccountRepository accountRepository, IUserRepository userRepository)
    {
        _accountRepository = accountRepository;
        _userRepository = userRepository;
    }

    /// <summary>
    /// Cria a conta e vincula ao usuário (atualiza users.account_id).
    /// </summary>
    public async Task<AccountResponse> ExecuteAsync(Guid userId, CreateAccountRequest request, CancellationToken cancellationToken = default)
    {
        // Valida existência do usuário
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException("Usuário");

        // Valida 1:1 — usuário não pode ter mais de uma conta ativa
        if (await _accountRepository.ExistsActiveByUserIdAsync(userId, cancellationToken))
            throw new DomainException("Usuário já possui uma conta ativa.");

        // Hash do CPF (SHA-256 em bytes para VARBINARY do MySQL)
        var cpfDigits = new string(request.HolderCpf.Where(char.IsDigit).ToArray());
        var cpfHashBytes = Sha256Helper.ComputeBytes(cpfDigits);

        // Cria a entidade
        var account = new Account(
            accountId: Guid.NewGuid(),
            userId: userId,
            holderName: request.HolderName,
            holderEmail: request.HolderEmail.Trim().ToLower(),
            holderCpfHash: cpfHashBytes
        );

        await _accountRepository.AddAsync(account, cancellationToken);

        // Vincula users.account_id → nova conta criada (preserva UserId original do JWT)
        await _userRepository.LinkAccountAsync(userId, account.AccountId, cancellationToken);

        return AccountResponseMapper.FromEntity(account);
    }
}

/// <summary>
/// Use Case: Consultar conta por ID.
/// </summary>
public sealed class GetAccountUseCase
{
    private readonly IAccountRepository _accountRepository;

    public GetAccountUseCase(IAccountRepository accountRepository)
    {
        _accountRepository = accountRepository;
    }

    public async Task<AccountResponse> ExecuteAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        var account = await _accountRepository.GetByIdAsync(accountId, cancellationToken)
            ?? throw new NotFoundException("Conta");

        return AccountResponseMapper.FromEntity(account);
    }
}

/// <summary>
/// Use Case: Atualizar o status da conta (Active/Blocked).
/// </summary>
public sealed class UpdateAccountStatusUseCase
{
    private readonly IAccountRepository _accountRepository;

    public UpdateAccountStatusUseCase(IAccountRepository accountRepository)
    {
        _accountRepository = accountRepository;
    }

    public async Task<AccountResponse> ExecuteAsync(
        Guid accountId,
        AccountStatus newStatus,
        CancellationToken cancellationToken = default)
    {
        var account = await _accountRepository.GetByIdAsync(accountId, cancellationToken)
            ?? throw new NotFoundException("Conta");

        if (account.Status == AccountStatus.Closed)
            throw new DomainException("Não é possível alterar o status de uma conta encerrada.");

        // Precisamos de uma entidade "attached" para Update — recarrega sem AsNoTracking
        var tracked = await _accountRepository.GetTrackedByIdAsync(accountId, cancellationToken)
            ?? throw new NotFoundException("Conta");

        tracked.UpdateStatus(newStatus);
        await _accountRepository.UpdateAsync(tracked, cancellationToken);

        return AccountResponseMapper.FromEntity(tracked);
    }
}

/// <summary>
/// Use Case: Listar todas as contas do sistema.
/// </summary>
public sealed class GetAllAccountsUseCase
{
    private readonly IAccountRepository _accountRepository;

    public GetAllAccountsUseCase(IAccountRepository accountRepository)
    {
        _accountRepository = accountRepository;
    }

    public async Task<IEnumerable<AccountResponse>> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var accounts = await _accountRepository.GetAllAsync(cancellationToken);
        return accounts.Select(AccountResponseMapper.FromEntity);
    }
}

/// <summary>
/// Use Case: Adicionar saldo a uma conta.
/// </summary>
public sealed class AddBalanceUseCase
{
    private readonly IAccountRepository _accountRepository;

    public AddBalanceUseCase(IAccountRepository accountRepository)
    {
        _accountRepository = accountRepository;
    }

    public async Task<AccountResponse> ExecuteAsync(
        Guid accountId,
        decimal amount,
        CancellationToken cancellationToken = default)
    {
        if (amount <= 0)
            throw new DomainException("Valor deve ser maior que zero.");

        var tracked = await _accountRepository.GetTrackedByIdAsync(accountId, cancellationToken)
            ?? throw new NotFoundException("Conta");

        if (tracked.Status != AccountStatus.Active)
            throw new DomainException("Não é possível adicionar saldo a uma conta inativa.");

        tracked.AddBalance(amount);
        await _accountRepository.UpdateAsync(tracked, cancellationToken);

        return AccountResponseMapper.FromEntity(tracked);
    }
}

/// <summary>
/// Use Case: Consultar extrato bancário com paginação por cursor.
/// Converte StatementRequest (DTO da API) para StatementQuery (contrato do repositório)
/// e StatementResult para StatementResponse.
/// </summary>
public sealed class GetStatementUseCase
{
    private readonly ITransactionRepository _transactionRepository;

    public GetStatementUseCase(ITransactionRepository transactionRepository)
    {
        _transactionRepository = transactionRepository;
    }

    public async Task<StatementResponse> ExecuteAsync(
        StatementRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = new StatementQuery(
            AccountId: request.AccountId,
            StartDate: request.StartDate,
            EndDate: request.EndDate,
            Cursor: request.Cursor,
            Limit: request.Limit);

        var result = await _transactionRepository.GetStatementAsync(query, cancellationToken);

        var transactions = result.Transactions.Select(t => new TransactionDto(
            TransactionId: t.TransactionId,
            EndToEndId: t.EndToEndId,
            Date: t.Date,
            Type: t.Direction == "DEBIT" ? TransactionType.Debit : TransactionType.Credit,
            Description: t.Description,
            Amount: t.Amount,
            Status: t.Status,
            CounterpartyName: t.CounterpartyName,
            CounterpartyDocument: t.CounterpartyDocument
        )).ToList();

        return new StatementResponse(
            Transactions: transactions,
            NextCursor: result.NextCursor,
            HasMore: result.HasMore
        );
    }
}

