using System.Net;
using System.Text.Json;
using BankingCore.Domain.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace BankingCore.API.Middleware;

/// <summary>
/// Middleware global de tratamento de exceções.
/// Captura todas as exceções não tratadas e retorna ProblemDetails (RFC 7807).
/// (CONSTITUTION Lei I.2: Zero PII em logs — nunca loga senhas, CPF, ou dados sensíveis)
/// (CONSTITUTION Lei RNF04: Tratamento global com ProblemDetails)
///
/// Em DEVELOPMENT: expõe detalhes reais (tipo da exceção + mensagem) para facilitar debug.
/// Em PRODUCTION: oculta detalhes internos (retorna mensagem genérica) para evitar vazamento.
/// </summary>
public class ExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _logger;
    private readonly IWebHostEnvironment _env;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ExceptionMiddleware(
        RequestDelegate next,
        ILogger<ExceptionMiddleware> logger,
        IWebHostEnvironment env)
    {
        _next = next;
        _logger = logger;
        _env = env;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            context.Response.StatusCode = 499; // Client Closed Request
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        // Mapeia exceções de domínio para status codes apropriados
        var (statusCode, title, detail) = exception switch
        {
            UnauthorizedException =>
                ((int)HttpStatusCode.Unauthorized, "Não autorizado", exception.Message),

            NotFoundException =>
                ((int)HttpStatusCode.NotFound, "Recurso não encontrado", exception.Message),

            Domain.Exceptions.ValidationException validationEx =>
                ((int)HttpStatusCode.UnprocessableEntity, "Erro de validação", validationEx.Message),

            DomainException =>
                ((int)HttpStatusCode.BadRequest, "Erro de negócio", exception.Message),

            SecurityTokenExpiredException =>
                ((int)HttpStatusCode.Unauthorized, "Token expirado", "O token JWT expirou. Faça login novamente."),

            SecurityTokenException =>
                ((int)HttpStatusCode.Unauthorized, "Token inválido", "O token JWT fornecido é inválido."),

            // Exceções de infraestrutura (DB, rede, etc.)
            _ => BuildGenericError(exception)
        };

        // Log seguro (sem PII — CONSTITUTION Lei I.2). Inclui Message para debugging.
        _logger.LogError(exception,
            "Exceção não tratada: {ExceptionType} — Status: {StatusCode}. Path: {Path}. Message: {Message}",
            exception.GetType().Name,
            statusCode,
            context.Request.Path,
            exception.Message);

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Instance = context.Request.Path,
            Type = $"https://httpstatuses.com/{statusCode}"
        };

        // Adiciona erros de validação se disponíveis
        if (exception is Domain.Exceptions.ValidationException validationException)
        {
            problemDetails.Extensions["errors"] = validationException.Errors;
        }

        // Em Development: expõe stack trace para facilitar debug de erros 500
        if (_env.IsDevelopment())
        {
            problemDetails.Extensions["exceptionType"] = exception.GetType().FullName;
            problemDetails.Extensions["innerException"] = exception.InnerException?.Message;
            problemDetails.Extensions["source"] = exception.Source;
        }

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";

        var json = JsonSerializer.Serialize(problemDetails, JsonOptions);
        await context.Response.WriteAsync(json);
    }

    /// <summary>
    /// Constrói resposta genérica para erros 500.
    /// Em Development: expõe a mensagem real da exceção para facilitar debug.
    /// Em Production: retorna mensagem genérica para não vazar informações internas.
    /// </summary>
    private (int status, string title, string detail) BuildGenericError(Exception exception)
    {
        if (_env.IsDevelopment())
        {
            return (
                (int)HttpStatusCode.InternalServerError,
                "Erro interno (DEV)",
                $"{exception.GetType().Name}: {exception.Message}"
            );
        }

        return (
            (int)HttpStatusCode.InternalServerError,
            "Erro interno",
            "Ocorreu um erro inesperado. Tente novamente mais tarde."
        );
    }
}
