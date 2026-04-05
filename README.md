# Projeto Final - Sistema de Gerenciamento de Tarefas

## Objetivo

Desenvolver uma API RESTful para gerenciamento de tarefas (Todo List) utilizando ASP.NET Core 8, seguindo as práticas de Git Flow para controle de versão.

## Tecnologias

- **Backend:** ASP.NET Core 8 (C#)
- **Banco de Dados:** SQL Server / SQLite
- **Versionamento:** Git Flow
- **Documentação:** Swagger/OpenAPI

## Estrutura do Projeto

```
proj-final-taik4/
├── src/
│   └── TodoApi/
│       ├── Controllers/
│       ├── Models/
│       ├── Services/
│       └── Program.cs
├── tests/
├── .gitignore
└── README.md
```

## Como Executar

```bash
dotnet run
```

Acesse o Swagger em: `http://localhost:5185/swagger`

## Git Flow

- **main:** Código em produção
- **develop:** Branch de integração
- **feature/\*:** Novas funcionalidades

## Autor

**Samuel Maciel Fonseca** - @taik4
