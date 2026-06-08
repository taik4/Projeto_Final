# TASKS.md - Cronograma de Execução

# Instrução: Gere o código completo, seguindo todas as leis do CONSTITUTION.md, sem omitir imports, com tratamento de erros adequado e comentários explicativos.

## 🏗️ FASE 1: Infraestrutura e Banco de Dados

### Task 1.1: Docker Compose Setup
**Objetivo:** Criar ambiente Docker com MySQL e Kafka (opcional)

- [ ] Criar arquivo `docker-compose.yml` na raiz do projeto
- [ ] Configurar serviço MySQL 8.0 com volume para script de inicialização
- [ ] Configurar serviço Kafka (ou documentar que será usado mock)
- [ ] Validar que `docker compose up` sobe os containers

**Prompt**
Crie um docker-compose.yml para meu Banking Core com:
MySQL 8.0 com senha root, porta 3306, database banking_core
Volume montando ./db/init.sql em /docker-entrypoint-initdb.d/
Kafka (opcional, pode ser comentado) com Zookeeper
Rede compartilhada entre os serviços
Inclua comentários explicando cada configuração.

**Validação:**
bash
docker compose up -d
docker ps 

### Task 1.2: Schema SQL e Stored Procedures

**Objetivo**: Criar schema completo do banco com lógica transacional

Criar pasta db/ na raiz

Criar arquivo db/init.sql com:

Criação do database banking_core

Tabelas accounts e transactions (com UUIDs binários)

Stored Procedure sp_process_pix_transfer (com idempotência)

View vw_account_statement (com mascaramento)

Trigger de auditoria

Validar que o script é executado automaticamente ao subir o container

**Prompt**
Gere o script SQL completo (db/init.sql) para MySQL 8 contendo:

1. CREATE DATABASE banking_core
2. Tabela accounts com:
   - account_id BINARY(16) UUID
   - holder_cpf_hash VARBINARY(64)
   - balance DECIMAL(15,2) com CHECK >= 0
   - status ENUM
3. Tabela transactions com:
   - end_to_end_id CHAR(32) UNIQUE
   - idempotency_key BINARY(16) UNIQUE
   - Foreign keys para accounts
4. Stored Procedure sp_process_pix_transfer que:
   - Valida idempotência
   - Faz FOR UPDATE NOWAIT na conta origem
   - Debita saldo atomicamente
   - Retorna status via parâmetros OUT
5. View vw_account_statement com mascaramento de CPF
6. Trigger AFTER INSERT em transactions para auditoria

Inclua comentários explicando cada decisão de design.

docker exec -it mysql_container mysql -u root -p
USE banking_core;
SHOW TABLES;

SHOW PROCEDURE STATUS WHERE Db = 'banking_core';

## 🏗️ FASE 2: Skeleton .NET e Autenticação
**Objetivo:** Criar a estrutura base do projeto .NET seguindo Clean Architecture e implementar todo o sistema de autenticação JWT

Criar solução .NET com Clean Architecture (4 projetos).

Implementar geração e validação de JWT RS256.

Criar endpoint de Login e Registro (com hash de CPF).

Configurar Middleware global de tratamento de exceções (ProblemDetails).

### Task 2.1: Solução e Projetos
**Objetivo:** Criar estrutura de projetos seguindo Clean Architecture

**Ações:**
Criar Solution BankingCore.sln

Criar 4 projetos Class Library/Web API:

BankingCore.Domain
BankingCore.Application
BankingCore.Infrastructure
BankingCore.API

Configurar referências entre projetos:

API → Application, Infrastructure
Infrastructure → Application, Domain
Application → Domain

**Prompt**
Gere os comandos .NET CLI para criar uma solução Clean Architecture:

1. dotnet new sln -n BankingCore
2. Criar 4 projetos:
   - BankingCore.Domain (Class Library)
   - BankingCore.Application (Class Library)
   - BankingCore.Infrastructure (Class Library)
   - BankingCore.API (Web API)
3. Adicionar todos à solution
4. Configurar referências:
   - API referencia Application e Infrastructure
   - Infrastructure referencia Application e Domain
   - Application referencia Domain

Forneça os comandos exatos para executar no terminal.

**Validação:**

dotnet build  # Deve compilar sem erros

### Task 2.2: Pacotes NuGet Essenciais
**Objetivo:** Instalar dependências necessárias em cada projeto

**Ações:**
Instalar pacotes no Infrastructure:

Pomelo.EntityFrameworkCore.MySql
Dapper
Confluent.Kafka (ou mock)

Instalar pacotes no API:

Swashbuckle.AspNetCore
Microsoft.AspNetCore.Authentication.JwtBearer

Instalar pacotes no Application:
FluentValidation
FluentValidation.DependencyInjectionExtensions

**Prompt**
Liste os comandos dotnet add package para instalar as dependências:

**Infrastructure:**
- Pomelo.EntityFrameworkCore.MySql (versão compatível com .NET 8)
- Dapper
- Confluent.Kafka

**API:**
- Swashbuckle.AspNetCore
- Microsoft.AspNetCore.Authentication.JwtBearer

**Application:**
- FluentValidation
- FluentValidation.DependencyInjectionExtensions

Forneça os comandos exatos para cada projeto.

**Validação:**
dotnet restore
dotnet build  # Deve compilar sem erros

### Task 2.3: Entidade User e Hashing
**Objetivo:** Implementar modelo de usuário com hash de senha

**Ações:**

Criar entidade User no Domain com propriedades:

Id (Guid)
Email (string)
PasswordHash (string)
CpfHash (string)

Criar interface IPasswordHasher no Domain

Implementar BcryptPasswordHasher no Application

Registrar serviço no DI container

**Prompt**
Implemente o sistema de usuários:

1. No projeto Domain, crie:
   - Entidade User com Id, Email, PasswordHash, CpfHash
   - Interface IPasswordHasher com métodos HashPassword e VerifyPassword

2. No projeto Application, crie:
   - Implementação BcryptPasswordHasher usando BCrypt.Net-Next

3. Forneça o código para registrar o serviço no Program.cs

Use BCrypt com work factor 12. Inclua validação de email e CPF.

**Validação:**
dotnet build  # Deve compilar

### Task 2.4: Serviço de JWT (RS256)
**Objetivo:** Implementar geração e validação de tokens JWT assimétricos

**Ações:**
Criar JwtSettings class para configuração
Criar interface IJwtService no Application
Implementar JwtService com:
Geração de chave RSA em memória (dev)
Método GenerateAccessToken(User user)
Método GenerateRefreshToken()
Configurar validação JWT no Program.cs da API
Adicionar settings no appsettings.json
**Prompt**
Implemente o sistema JWT RS256:

1. Crie JwtSettings class com Issuer, Audience, ExpirationMinutes
2. Crie interface IJwtService no Application
3. Implemente JwtService que:
   - Gera par RSA em memória (para dev)
   - Cria token com claims: sub (userId), email, role
   - Assina com RS256
   - Token expira em 15 minutos
4. Configure AddAuthentication/AddJwtBearer no Program.cs
5. Forneça appsettings.json com JwtSettings

Inclua comentários sobre como usar certificados em produção.
**Validação:**
dotnet build

### Task 2.5: Endpoints de Auth e Middleware de Erros
**Objetivo:** Criar API de autenticação e tratamento global de exceções

**Ações:**
Criar DTOs: LoginRequest, RegisterRequest, AuthResponse
Criar AuthController com endpoints:
POST /api/auth/register
POST /api/auth/login
Criar ExceptionMiddleware global que:
Captura exceções não tratadas
Retorna ProblemDetails (RFC 7807)
Loga erros (sem dados sensíveis)
Registrar middleware no pipeline
**Prompt**
Implemente a API de autenticação:

1. Crie DTOs no Application:
   - RegisterRequest (Email, Password, Cpf)
   - LoginRequest (Email, Password)
   - AuthResponse (AccessToken, RefreshToken, ExpiresAt)

2. Crie AuthController com:
   - POST /api/auth/register (cria usuário com CPF e senha hasheados)
   - POST /api/auth/login (valida credenciais e retorna JWT)

3. Crie ExceptionMiddleware que:
   - Captura todas exceções
   - Retorna ProblemDetails padrão
   - Loga erro sem expor dados sensíveis
   - Mapeia exceções específicas para status codes (400, 401, 404, etc)

4. Configure o middleware no Program.cs

Use FluentValidation para validar os requests.
**Validação:**
# Teste manual via Swagger ou curl
dotnet run
# Acesse http://localhost:5000/swagger
# Teste register e login

## 🏗️ FASE 3: CRUD de Contas e Swagger
**Objetivo:** Implementar o gerenciamento completo de contas bancárias usando EF Core e configurar a documentação Swagger.

**Checklist**
Implementar EF Core DbContext e Entidade Account.
Criar Controller de Contas (Create, Read, Update, Soft Delete).
Configurar Swashbuckle (Swagger) com XML Comments e exemplos.
Adicionar Policy de Autorização (Só dono vê a conta).

### Task 3.1: Repositório de Contas (EF Core)
**Objetivo:** Implementar persistência de contas usando Entity Framework Core

**Ações:**
Criar entidade Account no Domain com:
AccountId (Guid)
UserId (Guid, FK)
Balance (decimal)
Status (enum)
CreatedAt, UpdatedAt
Criar BankingDbContext no Infrastructure com:
Mapeamento de BINARY(16) para Guid
Configuração de relacionamentos
Criar interface IAccountRepository no Domain
Implementar AccountRepository no Infrastructure
Registrar DbContext e repositório no DI

**Prompt**
Implemente o repositório de contas com EF Core:

1. No Domain, crie entidade Account com:
   - AccountId (Guid)
   - UserId (Guid)
   - Balance (decimal)
   - Status (enum: Active, Blocked, Closed)
   - CreatedAt, UpdatedAt

2. No Infrastructure, crie BankingDbContext:
   - Configure mapeamento de Guid para BINARY(16) do MySQL
   - Configure relacionamento User -> Accounts
   - Use Pomelo.EntityFrameworkCore.MySql

3. No Domain, crie interface IAccountRepository com métodos:
   - CreateAsync(Account account)
   - GetByIdAsync(Guid accountId)
   - GetByUserIdAsync(Guid userId)
   - UpdateAsync(Account account)

4. No Infrastructure, implemente AccountRepository usando EF Core

5. Forneça código para registrar no Program.cs:
   - AddDbContext<BankingDbContext>
   - AddScoped<IAccountRepository, AccountRepository>

Inclua ConnectionString no appsettings.json.
**Validação:**
# Teste criando uma conta via código ou endpoint
dotnet build

### Task 3.2: Use Cases e Controller de Contas
**Objetivo:** Implementar lógica de negócio e endpoints de contas

**Ações:**
Criar Use Cases no Application:
CreateAccountUseCase
GetAccountUseCase
UpdateAccountUseCase
Criar validadores FluentValidation para cada Use Case
Criar AccountsController com endpoints:
POST /api/accounts
GET /api/accounts/{id}
PUT /api/accounts/{id}
DELETE /api/accounts/{id} (soft delete)
Implementar Policy de Autorização (usuário só acessa própria conta)

**Prompt**
Implemente o CRUD de contas:

1. No Application, crie Use Cases:
   - CreateAccountUseCase (valida se usuário já tem conta ativa)
   - GetAccountUseCase (retorna dados da conta)
   - UpdateAccountUseCase (atualiza status)
   
2. Crie validadores FluentValidation para cada Use Case

3. No API, crie AccountsController com:
   - POST /api/accounts (cria conta para usuário autenticado)
   - GET /api/accounts/{id} (retorna conta)
   - PUT /api/accounts/{id} (atualiza conta)
   - DELETE /api/accounts/{id} (soft delete, muda status para Closed)

4. Implemente autorização:
   - Usuário só pode acessar suas próprias contas
   - Use Policy ou verificação manual no controller

5. Configure Swagger com XML Comments

Retorne ProblemDetails em caso de erro (conta não encontrada, não autorizado, etc).

**Validação:**
# Teste via Swagger:
# 1. Faça login e pegue o token
# 2. Crie uma conta
# 3. Consulte a conta criada
# 4. Tente acessar conta de outro usuário (deve falhar)

### Task 3.3: Configuração do Swagger
**Objetivo:** Documentar API com Swagger/OpenAPI completo

**Ações:**
Habilitar XML Comments no projeto API
Adicionar comentários XML em todos Controllers e DTOs
Configurar Swagger para incluir XML file
Adicionar exemplos de Request/Response
Configurar autenticação JWT no Swagger (botão "Authorize")
Adicionar descrições de erros (400, 401, 404, 422)

**Prompt**
Configure Swagger completo:

1. No projeto API, habilite XML Comments:
   - Properties -> Build -> XML documentation file
   - Ou adicione <GenerateDocumentationFile>true</GenerateDocumentationFile> no .csproj

2. Adicione comentários XML em:
   - Todos Controllers (summary, remarks)
   - Todos DTOs (propriedades)
   - Todos endpoints (responses possíveis)

3. No Program.cs, configure AddSwaggerGen:
   - Inclua XML file
   - Adicione segurança JWT (AddSecurityDefinition, AddSecurityRequirement)
   - Configure exemplos de request/response

4. Adicione atributos [ProducesResponseType] em todos endpoints

5. Teste no Swagger UI:
   - Botão Authorize deve funcionar
   - Todos endpoints devem ter documentação
   - Exemplos devem aparecer

Inclua descrição detalhada do fluxo de paginação por cursor no endpoint de extrato.

**Validação:**
dotnet run
# Acesse http://localhost:5000/swagger
# Valide que todos endpoints estão documentados
# Teste o botão Authorize

## 🏗️ FASE 4: Core Transacional PIX

**Objetivo:** Implementar o coração do sistema bancário: transferências PIX com alta performance, consistência e idempotência usando Stored Procedures.

**Checklist**
Implementar Repositório Dapper para chamar sp_process_pix_transfer.
Criar Use Case de Transferência PIX com FluentValidation.
Implementar IEventPublisher (Kafka ou Mock).
Testar fluxo de débito e idempotência via Postman/Swagger.

### Task 4.1: Repositório de Transações (Dapper)
**Objetivo:** Implementar repositório de transações usando Dapper e Stored Procedures

**Ações:**
Criar entidade Transaction no Domain
Criar DTOs de resultado: PixTransferResult
Criar interface ITransactionRepository no Domain com método:
ProcessTransferAsync(...)
Implementar TransactionRepository no Infrastructure usando Dapper
Mapear parâmetros da Stored Procedure para C#

**Prompt**
Implemente o repositório de transações com Dapper:

1. No Domain, crie:
   - Entidade Transaction com todas propriedades da tabela
   - DTO PixTransferResult com Status, Message, NewBalance, EndToEndId
   - Interface ITransactionRepository com método:
     Task<PixTransferResult> ProcessTransferAsync(
       Guid senderAccountId,
       string receiverKey,
       decimal amount,
       string endToEndId,
       Guid idempotencyKey,
       string description
     )

2. No Infrastructure, implemente TransactionRepository:
   - Use Dapper (não EF Core)
   - Chame a Stored Procedure sp_process_pix_transfer
   - Mapeie parâmetros IN e OUT
   - Use MySqlConnection do Pomelo
   - Converta Guid para BINARY(16) e vice-versa

3. Registre o repositório no DI

Inclua tratamento de exceções do MySQL (MySqlException).

**Validação:**
dotnet build

### Task 4.2: Use Case de Transferência PIX
**Objetivo:** Implementar lógica de negócio da transferência PIX

**Ações:**
Criar DTOs: PixTransferRequest, PixTransferResponse
Criar TransferPixUseCase no Application que:
Valida input com FluentValidation
Gera EndToEndId único se não fornecido
Gera IdempotencyKey se não fornecida
Chama repositório Dapper
Trata diferentes status (SUCESSO, SALDO_INSUFICIENTE, DUPLICADA)
Publica evento de transferência concluída
Criar validador PixTransferValidator
Criar endpoint POST /api/pix/transfer no API
**Prompt**
Implemente o Use Case de transferência PIX:

1. No Application, crie:
   - PixTransferRequest (SenderAccountId, ReceiverKey, Amount, Description, IdempotencyKey?)
   - PixTransferResponse (Status, EndToEndId, NewBalance, Message)
   - PixTransferValidator (FluentValidation):
     * Amount > 0
     * ReceiverKey não vazio
     * SenderAccountId válido
   
2. Crie TransferPixUseCase que:
   - Valida request com PixTransferValidator
   - Gera EndToEndId (formato: E + 31 chars alfanuméricos) se não fornecido
   - Gera IdempotencyKey (Guid) se não fornecida
   - Chama ITransactionRepository.ProcessTransferAsync
   - Trata resultados:
     * SETTLED -> sucesso
     * REJECTED -> retorna erro com motivo
     * DUPLICATE -> retorna resultado da transação original
   - Publica evento PixTransferCompletedEvent (pode ser mock por agora)

3. No API, crie PixController com:
   - POST /api/pix/transfer
   - Usa [Authorize]
   - Extrai UserId do JWT
   - Retorna 200 com response ou 400/422 com ProblemDetails

Inclua logs de auditoria (sem dados sensíveis).
**Validação:**
# Teste via Swagger:
# 1. Crie duas contas
# 2. Adicione saldo manualmente no banco (UPDATE accounts SET balance = 1000)
# 3. Faça transferência da conta 1 para conta 2
# 4. Verifique saldo atualizado
# 5. Tente mesma transferência novamente (deve retornar DUPLICATE)

### Task 4.3: Publicador de Eventos (In-Memory Mock)
**Objetivo:**  Implementar sistema de eventos com mock para garantir entrega

**Ações:**
Criar interface IEventPublisher no Application
Criar evento PixTransferCompletedEvent no Application
Implementar InMemoryEventPublisher que apenas loga no console
(Opcional) Implementar KafkaEventPublisher como alternativa
Injetar IEventPublisher no TransferPixUseCase
Publicar evento após transferência bem-sucedida
**Prompt**
Implemente o sistema de eventos:

1. No Application, crie:
   - Interface IEventPublisher com método PublishAsync<T>(T event)
   - Classe PixTransferCompletedEvent com propriedades:
     * EventId (Guid)
     * Timestamp (DateTime)
     * EndToEndId (string)
     * SenderAccountId (Guid)
     * Amount (decimal)
     * Status (string)

2. Crie InMemoryEventPublisher que:
   - Implementa IEventPublisher
   - Apenas loga o evento no console (ILogger)
   - Simula latência de 10ms (Task.Delay)

3. (Opcional) Crie KafkaEventPublisher que:
   - Usa Confluent.Kafka
   - Publica no tópico "pix-transfers"
   - Serializa evento como JSON

4. Injete IEventPublisher no TransferPixUseCase
5. Publique evento após transferência SETTLED
6. Registre InMemoryEventPublisher no DI (pode adicionar feature flag para Kafka depois)

Inclua comentários sobre como escalar para Kafka em produção.

**Validação:**
# Faça uma transferência e verifique o log no console mostrando o evento publicado
dotnet run

## 🏗️ FASE 5: Extrato e Transparência
**Objetivo:** Implementar consulta de extrato bancário com paginação eficiente, transparência PIX (E2E ID) e mascaramento de dados sensíveis.

**Checklist**
Implementar consulta à View vw_account_statement via Dapper.
Criar endpoint de Extrato com paginação por cursor (Keyset).
Validar se o E2E ID e dados mascarados estão retornando corretamente.
Congelar código de novas features (Feature Freeze).

### Task 5.1: Consulta de Extrato (View + Dapper)
**Objetivo:** 
Implementar consulta de extrato com paginação por cursor

**Ações:**
Adicionar método GetStatementAsync na interface ITransactionRepository
Implementar método no repositório usando Dapper:
Consulta a View vw_account_statement
Usa paginação por cursor (WHERE transaction_id < @Cursor)
Aplica filtros de data
Criar DTOs: StatementRequest, StatementResponse, TransactionDto

**Prompt**
Implemente a consulta de extrato:

1. No Domain, adicione à ITransactionRepository:
   Task<StatementResponse> GetStatementAsync(
     Guid accountId,
     DateTime? startDate,
     DateTime? endDate,
     string cursor,
     int limit
   )

2. Crie DTOs no Application:
   - StatementRequest (AccountId, StartDate?, EndDate?, Cursor?, Limit = 50)
   - StatementResponse (Transactions[], NextCursor, HasMore)
   - TransactionDto (TransactionId, EndToEndId, Date, Type, Description, Amount, Status, CounterpartyName)

3. No Infrastructure, implemente GetStatementAsync:
   - Use Dapper para consultar vw_account_statement
   - Filtre por AccountId (sender ou receiver)
   - Aplique filtros de data se fornecidos
   - Use paginação por cursor: WHERE transaction_id < @Cursor
   - Ordene por initiated_at DESC
   - Limite resultados ao valor de limit
   - Calcule NextCursor e HasMore

4. Converta BINARY(16) para Guid no mapeamento

Inclua validação de parâmetros (limit máximo 100, datas válidas).

**Validação:**
dotnet build

### Task 5.2: Endpoint de Extrato 
**Objetivo:** Criar endpoint REST para consulta de extrato

**Ações:**
Criar GetStatementUseCase no Application
Criar validador StatementValidator
Adicionar endpoint GET /api/accounts/{id}/statement no AccountsController
Implementar paginação por cursor nos parâmetros de query
Configurar Swagger com exemplos
**Prompt**
Implemente o endpoint de extrato:

1. No Application, crie:
   - GetStatementUseCase que chama ITransactionRepository.GetStatementAsync
   - StatementValidator (FluentValidation):
     * Limit entre 1 e 100
     * StartDate <= EndDate se ambos fornecidos
     * Cursor é Guid válido se fornecido

2. No API, adicione ao AccountsController:
   - GET /api/accounts/{id}/statement
   - Parâmetros de query: startDate, endDate, cursor, limit
   - Usa [Authorize]
   - Valida se usuário é dono da conta
   - Retorna 200 com StatementResponse
   - Retorna 400 se validação falhar
   - Retorna 404 se conta não existir

3. Configure Swagger com:
   - XML Comments explicando paginação por cursor
   - Exemplos de request e response
   - Descrição do formato do cursor

Inclua logs de acesso ao extrato (quem consultou, quando, IP).

**Validação:**
# Teste via Swagger:
# 1. Faça algumas transferências
# 2. Consulte extrato sem filtros
# 3. Consulte com filtros de data
# 4. Teste paginação (use NextCursor da resposta anterior)
# 5. Verifique se dados estão mascarados

## 🏗️ FASE 6: Testes Automatizados
**Objetivo:** 
Implementar testes de integração robustos usando Testcontainers, cobrindo os 5 casos críticos exigidos pela faculdade.
**Checklist**
Configurar xUnit e Testcontainers no projeto de testes.
Escrever Teste 1: Transferência com sucesso.
Escrever Teste 2: Saldo insuficiente.
Escrever Teste 3: Idempotência (Retry).
Escrever Teste 4: Extrato paginado.
Escrever Teste 5: Acesso negado (IDOR).

### Task 6.1: Setup Testcontainers
**Objetivo:** 
Configurar ambiente de testes com MySQL real em container
**Ações:**
Criar projeto BankingCore.IntegrationTests (xUnit)
Instalar pacotes:
xunit
xunit.runner.visualstudio
Testcontainers.MySql
Microsoft.NET.Test.Sdk
Criar classe MySqlContainerFixture que:
Sobe container MySQL antes dos testes
Executa script init.sql
Derruba container após testes
Configurar connection string para o container

**Prompt**
Configure Testcontainers para testes de integração:

1. Crie projeto BankingCore.IntegrationTests (xUnit)

2. Instale pacotes:
   - xunit
   - xunit.runner.visualstudio
   - Testcontainers.MySql
   - Microsoft.NET.Test.Sdk
   - Dapper
   - Pomelo.EntityFrameworkCore.MySql

3. Crie classe MySqlContainerFixture que:
   - Implementa IAsyncLifetime
   - Sobe container MySQL 8.0 no InitializeAsync
   - Lê e executa script ../../../../db/init.sql
   - Expõe ConnectionString
   - Derruba container no DisposeAsync

4. Crie classe base IntegrationTestBase que:
   - Usa [Collection("Database")]
   - Injeta MySqlContainerFixture
   - Fornece método para criar conexão MySqlConnection

5. Configure xunit.collection.fixture no AssemblyInfo

Inclua exemplo de teste simples que valida conexão.

**Validação:**
cd BankingCore.IntegrationTests
dotnet test  # Deve passar (teste de conexão)

### Task 6.2: Implementar os 5 Testes Críticos
**Objetivo:** 
Escrever testes de integração cobrindo fluxos principais

**Ações:**
Teste 1: Criar conta com sucesso
Teste 2: Login e geração de token JWT
Teste 3: Transferência PIX com saldo suficiente
Teste 4: Transferência PIX com saldo insuficiente
Teste 5: Idempotência (mesma transferência 2x não debita 2x)

**Prompt**
Implemente 5 testes de integração críticos:

Use a classe IntegrationTestBase e MySqlContainerFixture configurados anteriormente.

1. Teste: CreateAccount_Success
   - Cria usuário
   - Cria conta via repositório
   - Valida que conta existe no banco
   - Valida saldo inicial = 0

2. Teste: Login_ValidCredentials_ReturnsToken
   - Cria usuário com senha hasheada
   - Chama serviço de login
   - Valida que token JWT é retornado
   - Valida que token contém claims corretos

3. Teste: TransferPix_SufficientBalance_Success
   - Cria conta com saldo 1000
   - Executa transferência de 100
   - Valida status SETTLED
   - Valida saldo final = 900 no banco
   - Valida que transação foi registrada

4. Teste: TransferPix_InsufficientBalance_Rejected
   - Cria conta com saldo 50
   - Tenta transferência de 100
   - Valida status REJECTED
   - Valida saldo permanece 50 no banco
   - Valida que transação foi registrada como REJECTED

5. Teste: TransferPix_Idempotency_NoDoubleDebit
   - Cria conta com saldo 1000
   - Executa transferência de 100 com IdempotencyKey X
   - Valida saldo = 900
   - Executa MESMA transferência (mesma IdempotencyKey X)
   - Valida status DUPLICATE
   - Valida saldo AINDA = 900 (não debitou novamente)

Use Dapper para validar estado do banco diretamente.
Inclua [Fact] e nomes descritivos.

**Validação:**
cd BankingCore.IntegrationTests
dotnet test --verbosity normal
dotnet test tests/BankingCore.IntegrationTests --verbosity normal
# Todos os 5 testes devem passar