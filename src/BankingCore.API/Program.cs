using BankingCore.Application.DTOs;
using BankingCore.Application.Events;
using BankingCore.Application.Settings;
using BankingCore.Application.Services;
using BankingCore.Application.UseCases;
using BankingCore.API.Middleware;
using BankingCore.Domain.Interfaces;
using BankingCore.Infrastructure.Data;
using BankingCore.Infrastructure.Repositories;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using MySqlConnector;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;
var builder = WebApplication.CreateBuilder(args);

// ============================================================
// 1. Database — EF Core com Pomelo MySQL
// ============================================================
// Connection string: variável de ambiente tem prioridade total
// (permite porta dinâmica, senhas, etc. sem editar appsettings).
var connectionString = Environment.GetEnvironmentVariable("MYSQL_CONNECTION_STRING")
    ?? builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "Connection string do MySQL não encontrada. " +
        "Defina MYSQL_CONNECTION_STRING ou ConnectionStrings:DefaultConnection.");

builder.Services.AddDbContext<BankingDbContext>(options =>
    options.UseMySql(
        connectionString,
        // Versão fixa: evita abrir conexão real no startup (que crasharia
        // a aplicação se o MySQL estiver offline). O Pomelo usa esse valor
        // para otimizar SQL gerado; MySQL 8.0+ é compatível.
        new MySqlServerVersion("8.0.36"),
        mySqlOptions => mySqlOptions.EnableRetryOnFailure(
            maxRetryCount: 3,
            maxRetryDelay: TimeSpan.FromSeconds(5),
            errorNumbersToAdd: null)));

// ============================================================
// 2. Authentication — JWT RS256
// ============================================================
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection(nameof(JwtSettings)));

// JwtService é singleton porque mantém o RSA em memória.
builder.Services.AddSingleton<JwtService>();

// Registra a instância concreta também como IJwtService para a interface.
builder.Services.AddSingleton<BankingCore.Application.Interfaces.IJwtService>(
    sp => sp.GetRequiredService<JwtService>());

// Configura AddAuthentication/AddJwtBearer com a chave pública do RSA (RS256).
var jwtSettings = builder.Configuration
    .GetSection(nameof(JwtSettings))
    .Get<JwtSettings>() ?? new JwtSettings();

builder.Services
    .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<JwtService>((options, jwtService) =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidAudience = jwtSettings.Audience,
            IssuerSigningKey = jwtService.GetSecurityKey(),
            ClockSkew = TimeSpan.Zero
        };
        options.MapInboundClaims = false;
    });

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer();

// ============================================================
// 3. Repositórios e Serviços (CONSTITUTION Lei III.2: DI por construtor)
// ============================================================
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IAccountRepository, AccountRepository>();
builder.Services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();
builder.Services.AddScoped<AuthUseCases>();

// Use Cases da Fase 3 — Contas bancárias
builder.Services.AddScoped<CreateAccountUseCase>();
builder.Services.AddScoped<GetAccountUseCase>();
builder.Services.AddScoped<GetAllAccountsUseCase>();
builder.Services.AddScoped<UpdateAccountStatusUseCase>();
builder.Services.AddScoped<AddBalanceUseCase>();

// Use Case da Fase 5 — Extrato bancário
builder.Services.AddScoped<GetStatementUseCase>();

// ============================================================
// 3b. Fase 4 — Core Transacional PIX (Dapper + SP + Eventos)
// ============================================================
// MySqlConnection como Scoped: uma conexão por request HTTP.
// Não é pool-friendly compartilhar como Singleton (CONSTITUTION Lei III.3: async/await total).
builder.Services.AddScoped(sp =>
    new MySqlConnection(connectionString));

builder.Services.AddScoped<ITransactionRepository, TransactionRepository>();

// Publicador de eventos: InMemory para dev, Kafka em produção (PLAN §5).
// Para trocar: substitua o registro por KafkaEventPublisher após configurar o broker.
builder.Services.AddSingleton<IEventPublisher, InMemoryEventPublisher>();

// Use Case de transferência PIX
builder.Services.AddScoped<TransferPixUseCase>();

// ============================================================
// 4. Validators (FluentValidation — registrado por assembly scanning)
// ============================================================
builder.Services.AddValidatorsFromAssemblyContaining<RegisterRequestValidator>();
builder.Services.AddValidatorsFromAssemblyContaining<PixTransferRequestValidator>();
builder.Services.AddValidatorsFromAssemblyContaining<BankingCore.Application.Validators.StatementRequestValidator>();

// ============================================================
// 5. Controllers e Swagger
// ============================================================
builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "LiceBank Banking Core API",
        Version = "v1",
        Description = "API de Core Banking com foco em transferências PIX e autenticação JWT RS256.",
        Contact = new OpenApiContact { Name = "LiceBank Team" }
    });

    // Suporte a Bearer Token no Swagger
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Insira o token JWT. Exemplo: eyJhbGciOiJSUzI1NiIs..."
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });

    // XML Comments do C# no Swagger
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
        options.IncludeXmlComments(xmlPath);
});

// ============================================================
// 6. Build e Pipeline de Middleware
// ============================================================
var app = builder.Build();

// Middleware global de exceções — deve ser o PRIMEIRO para capturar tudo
app.UseMiddleware<ExceptionMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "LiceBank API v1");
    });
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// ============================================================
// 7. (Dev) Tentar aplicar migrações ao iniciar
// ============================================================
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<BankingDbContext>();
    try
    {
        // O schema principal é criado via db/init.sql (Docker).
        // Aqui o EF apenas valida conectividade e cria tabelas extras (users).
        if (dbContext.Database.GetPendingMigrations().Any())
            dbContext.Database.Migrate();
        else
            dbContext.Database.EnsureCreated();
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogWarning(ex,
            "Não foi possível aplicar migrações/criar tabelas. " +
            "Verifique se o MySQL está online (docker compose up -d).");
    }
}

app.Run();

// Expõe Program para WebApplicationFactory (testes de integração)
public partial class Program { }
