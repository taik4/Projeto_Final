# SPEC.md - Banking Core com Transparência PIX

## 1. Visão Geral
Sistema de núcleo bancário (Core Banking) focado em processamento de transações PIX com transparência regulatória. O sistema utiliza uma abordagem híbrida onde a consistência forte reside em Stored Procedures no MySQL, enquanto a orquestração e integrações são gerenciadas pelo .NET 8.

## 2. Regras de Negócio (RN)
- **RN01 - Atomicidade:** Toda transferência PIX deve ser atômica. Ou debita e registra, ou reverte tudo.
- **RN02 - Idempotência:** O sistema não pode processar a mesma transferência duas vezes, mesmo em caso de retry de rede.
- **RN03 - Saldo Positivo:** Contas não podem ter saldo negativo (sem cheque especial neste escopo).
- **RN04 - Transparência PIX:** Todo response de transação/extrato deve conter o `EndToEndId` (E2E ID) e dados do recebedor mascarados.
- **RN05 - Imutabilidade de Extrato:** O nome do recebedor e a descrição devem ser "congelados" (snapshot) no momento da transação.

## 3. Requisitos Funcionais (RF)
- **RF01:** Cadastro e Login de usuários com emissão de JWT (RS256).
- **RF02:** CRUD completo de Contas Bancárias (Create, Read, Update, Soft Delete).
- **RF03:** Transferência PIX interna (débito e crédito) via Stored Procedure.
- **RF04:** Consulta de Extrato Bancário com paginação por cursor (keyset).
- **RF05:** Consulta de Saldo em tempo real.
- **RF06:** Publicação de evento de transação concluída (Kafka/Mock).

## 4. Requisitos Não Funcionais (RNF)
- **RNF01:** Backend em C# .NET 8 com Clean Architecture.
- **RNF02:** Banco de dados MySQL 8 rodando em Docker.
- **RNF03:** Uso de ORM (EF Core) para CRUD e Dapper para SPs.
- **RNF04:** Tratamento global de exceções retornando `ProblemDetails` (RFC 7807).
- **RNF05:** Swagger documentado com exemplos de request/response.
- **RNF06:** Mínimo de 5 testes automatizados (Integração com Testcontainers).

## 5. Contratos de API (Endpoints Principais)
- `POST /api/auth/login` -> Retorna JWT.
- `POST /api/accounts` -> Cria nova conta.
- `GET /api/accounts/{id}` -> Retorna dados da conta.
- `POST /api/pix/transfer` -> Executa SP de transferência.
- `GET /api/accounts/{id}/statement` -> Retorna extrato paginado.