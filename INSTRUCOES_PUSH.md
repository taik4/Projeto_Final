# Instruções para Push ao GitHub

## 1. Criar o Repositório no GitHub

1. Acesse: **https://github.com/new**
2. Preencha:
   - **Repository name:** `proj-final-taik4`
   - **Description:** `Projeto Final - API de Gerenciamento de Tarefas`
   - **Visibility:** Private (ou Public)
   - **NÃO marque** "Add README" ou ".gitignore"
3. Clique em **Create repository**

## 2. Configurar Remote e Push

Abra o terminal na pasta do projeto e execute:

```bash
cd "C:\Users\Usuario\Desktop\Faculdade\3-Semestre\Desenvolvimento-de-Sistemas\Tarefas\gitflow\proj-final-taik4"

# Configurar remote SSH
git remote add origin git@github.com:taik4/proj-final-taik4.git

# Push de todas as branches
git push -u origin master
git push origin develop
git push origin feature/health-endpoint
git push origin feature/improve-health-endpoint
git push origin release/1.0.0

# Push da tag
git push origin v1.0.0

# Ou push de tudo de uma vez:
git push --all origin
git push --tags origin
```

## 3. Verificar no GitHub

Acesse: **https://github.com/taik4/proj-final-taik4**

Verifique:
- [ ] README.md visível
- [ ] Branches: master, develop, feature/health-endpoint, feature/improve-health-endpoint, release/1.0.0
- [ ] Tags: v1.0.0
- [ ] Commits com mensagens corretas

## 4. Criar Pull Requests no GitHub

### PR 1: feature/health-endpoint → develop
1. Vá em **Pull requests → New pull request**
2. **base:** develop | **compare:** feature/health-endpoint
3. Título: `feat: add /health endpoint returning status and timestamp`
4. Descrição explicando o que foi feito
5. Clique em **Create pull request**
6. Faça merge

### PR 2: feature/improve-health-endpoint → develop
1. **base:** develop | **compare:** feature/improve-health-endpoint
2. Título: `feat: health endpoint now returns app version from configuration`
3. Descrição explicando a melhoria
4. Adicione: `Closes #1` (se tiver issue criada)
5. Clique em **Create pull request**
6. Faça merge

## 5. Adicionar Colaborador

1. Vá em **Settings → Collaborators**
2. Clique em **Add people**
3. Digite o usuário GitHub do colega
4. Selecione permissão **Write**

## 6. Criar Release

1. Vá em **Releases → Create a new release**
2. **Tag:** v1.0.0 (já existe)
3. **Title:** Initial Release
4. **Description:** Primeira versão com health endpoint funcional
5. Clique em **Publish release**
