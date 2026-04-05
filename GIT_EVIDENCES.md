# Evidências Git - Issue e PR Simuladas

> Como não foi possível criar Issues e PRs reais no GitHub sem autenticação, 
> documentamos abaixo o conteúdo que seria criado.

---

## Issue #1: Implementar Health Check Endpoint

**Título:** `feat: Implement /health endpoint for API monitoring`

**Descrição da Issue:**

### Contexto
Precisamos de um endpoint de health check para monitorar o status da API e permitir que serviços de orchestrator verifiquem se a aplicação está funcionando corretamente.

### Requisitos
- [ ] Criar endpoint `GET /api/health`
- [ ] Retornar status da aplicação (ok/error)
- [ ] Incluir timestamp da requisição
- [ ] Incluir versão da aplicação
- [ ] Adicionar logging de acesso

### Critérios de Aceitação
```json
{
  "status": "ok",
  "timestamp": "2026-04-05T14:30:00Z",
  "version": "1.0.0",
  "application": "TodoApi"
}
```

### Labels
`enhancement`, `good first issue`, `backend`

---

## Pull Request #1: feat: add /health endpoint

**Título:** `feat: add /health endpoint returning status and timestamp`

**Descrição da PR:**

### O que foi feito
- Criado controller `HealthController` com endpoint GET `/api/health`
- Criado model `HealthResponse` com campos: status, timestamp, version, application
- Configurado logging para registrar acessos ao endpoint

### Motivação
Permitir monitoramento da API e health checks para deploy em containers.

### Como testar
```bash
curl http://localhost:5185/api/health
```

### Linked Issues
Closes #1

---

## Pull Request #2: feat: improve /health endpoint

**Título:** `feat: health endpoint now returns app version from configuration`

**Descrição da PR:**

### O que foi alterado
- Refatorado HealthController para ler versão e nome do app do appsettings.json
- Removido hardcoding de valores

### Motivação
Permitir que clientes saibam qual versão do backend está em execução sem recompilar.

### Como testar
```bash
# Requisição GET /health retorna:
{ 
  "status": "ok", 
  "version": "1.0.0",
  "application": "TodoApi",
  "timestamp": "2026-04-05T..." 
}
```

### Linked Issues
Closes #1

---

## Review Comments (Simulado)

**Aluno A (owner):**
> ✅ LGTM! Boa melhoria, agora a versão é configurável. Aprovado.

**Aluno B (contributor):**
> Obrigado! Ajustei conforme solicitado.
