# CONSTITUTION.md - Leis do Banking Core

Este documento dita as regras inegociáveis de desenvolvimento. Qualquer código que viole estas leis deve ser refatorado imediatamente.

## I. Leis de Segurança (Security First)
1. **Nunca confie no cliente:** Todo input deve ser validado na borda (API) com FluentValidation e no banco (Constraints/SPs).
2. **Zero PII em Logs:** CPF, senhas e dados de cartão NUNCA podem ser logados. Use apenas hashes ou IDs internos.
3. **Mascaramento na Origem:** Dados sensíveis de terceiros (recebedor do PIX) devem ser mascarados dentro do MySQL (View/Function), não na API.
4. **Autorização Explícita:** Todo endpoint que recebe um `AccountId` na rota deve validar se o `User.Id` do JWT é o dono da conta.

## II. Leis de Banco de Dados (ACID & Performance)
1. **Lógica Financeira no Banco:** Débitos, créditos e validações de saldo ocorrem APENAS via Stored Procedures. A API apenas orquestra.
2. **Idempotência é Obrigatória:** Toda transação financeira exige uma `Idempotency-Key`. O banco deve rejeitar duplicatas silenciosamente ou retornar o status original.
3. **Sem Locks Infinitos:** Uso obrigatório de `FOR UPDATE NOWAIT` ou timeout curto em locks pessimistas para evitar deadlocks.
4. **ORM para CRUD, Dapper para Dinheiro:** EF Core é proibido no fluxo crítico de transferência PIX.

## III. Leis de Código C# (Clean Code)
1. **Result Pattern ou Exceptions:** Use Exceptions para falhas de infraestrutura e `Result<T>` (ou ProblemDetails) para falhas de negócio (ex: saldo insuficiente).
2. **Injeção de Dependência:** Toda dependência deve ser injetada via construtor. `new` é proibido para serviços e repositórios.
3. **Async/Await Total:** Nenhuma operação de I/O (Banco, Kafka, HTTP) pode ser síncrona. Use `CancellationToken` em todos os métodos de banco.
4. **Controllers Magros:** Controllers não contêm lógica de negócio. Eles apenas recebem o DTO, validam, chamam o Use Case e retornam o HTTP Response.

## IV. Leis de Testes e Qualidade
1. **Testes de Integração Reais:** Testes do core financeiro devem usar `Testcontainers` para subir um MySQL real. Mockar o banco em testes financeiros é proibido.
2. **Cobertura dos 5 Casos:** Os 5 testes definidos no TASKS.md são obrigatórios para a entrega.
3. **Swagger como Contrato:** O Swagger deve ser gerado a partir dos XML Comments do C#. Se não está no Swagger, não existe.

## V. Leis de Infraestrutura
1. **Tudo em Docker:** O projeto deve rodar com um único comando: `docker compose up`.
2. **Secrets fora do Código:** Senhas de banco e chaves JWT nunca são hardcodadas. Devem estar no `docker-compose.yml` (para dev) ou `.env` (ignorado no Git).