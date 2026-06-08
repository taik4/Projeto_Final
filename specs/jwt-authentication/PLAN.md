# PLAN.md - Arquitetura e Design

## 1. Stack Tecnológica
- **Linguagem:** C# .NET 8
- **Framework:** ASP.NET Core Web API
- **Banco de Dados:** MySQL 8.0 (InnoDB)
- **Acesso a Dados:** Entity Framework Core (CRUD) + Dapper (Stored Procedures)
- **Mensageria:** Apache Kafka (ou Mock In-Memory para garantir entrega)
- **Infraestrutura:** Docker & Docker Compose
- **Testes:** xUnit + Testcontainers

## 2. Padrão Arquitetural (Clean Architecture)
- **Domain:** Entidades (Account, Transaction), Enums, Interfaces de Repositório.
- **Application:** Use Cases (Handlers), DTOs, FluentValidation, Contratos de Eventos.
- **Infrastructure:** Implementação de Repositórios (EF Core/Dapper), Kafka Publisher, MySQL Context.
- **API:** Controllers, Middleware de Erros, Configuração de JWT e Swagger.

## 3. Estratégia de Dados (Híbrida)
- **ORM (EF Core):** Usado para migrações, seed de dados e operações simples de CRUD (ex: criar conta, buscar usuário).
- **Dapper + SPs:** Usado para o core financeiro (transferências, extrato). Justificativa: Garante atomicidade cross-table, locks pessimistas (`FOR UPDATE`) e performance sub-200ms que o EF Core não otimizaria nativamente.

## 4. Segurança e Compliance (SOC Posture)
- **Auth:** JWT RS256 (Assimetria). Chaves geradas e salvas em variáveis de ambiente/Docker Secrets.
- **Dados Sensíveis:** CPF armazenado apenas como Hash (SHA-256).
- **Mascaramento:** Aplicado nativamente no MySQL via Function (`fn_mask_document`) e View. A API nunca recebe o dado pleno.
- **Autorização:** Policy-based authorization no .NET para garantir que o usuário só acesse sua própria conta (Previne IDOR).

## 5. Estratégia de Mensageria (Event-Driven)
- Para o escopo acadêmico de 7 dias, a interface `IEventPublisher` terá duas implementações:
  1. `KafkaPublisher` (Real, usando Confluent.Kafka).
  2. `InMemoryPublisher` (Fallback, loga no console caso o container do Kafka falhe, garantindo que a API não trave e a transação seja salva).