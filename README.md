**Project API — Configuration & Code Delivery Guide**

Resumo rápido
- Arquivos de configuração por ambiente ficam em `source/WebApi/appsettings.{Environment}.json`.
- Valores sensíveis (senhas, chaves) NÃO devem ser comitados; use `dotnet user-secrets`, variáveis de ambiente, Docker secrets ou um secret manager (Azure Key Vault, AWS Secrets Manager).

AppSettings por ambiente
- `source/WebApi/appsettings.Development.json` — valores locais de exemplo (usar para desenvolvimento).
- `source/WebApi/appsettings.Staging.json` — placeholders para staging; **substituir** via CI/CD.
- `source/WebApi/appsettings.Production.json` — placeholders para produção; **NÃO** comitar segredos reais.

Como aplicar configurações seguras
1. No desenvolvimento local: use `dotnet user-secrets` para segredos sensíveis:

```powershell
cd source/WebApi
dotnet user-secrets init
dotnet user-secrets set "JwtSettings:Secret" "dev-secret-..."
dotnet user-secrets set "Email:Password" "smtp-password"
```

2. Em Docker/CI: injete variáveis de ambiente ou use Docker secrets:

```yaml
# exemplo docker-compose snippet
services:
  api:
    image: myapi:latest
    environment:
      - JwtSettings__Secret=${JWT_SECRET}
      - ConnectionStrings__DefaultConnection=${DB_CONN}
    secrets:
      - prod_db_password
```

3. Em cloud: use Key Vault / Secrets Manager e remapeie como variáveis de ambiente no runtime.

Override e precedência
- ASP.NET Core carrega `appsettings.json` → `appsettings.{ENV}.json` → environment variables → user secrets. Configure segredos onde for mais seguro.

Guia mínimo de commits e envio de código (branching & PR)
- Branches: `main` (produção), `develop` (integração), `feature/<ticket>` (trabalho). Use `hotfix/<id>` para correções urgentes.
- Commits: mensagens curtas no imperativo: `Add RabbitMQ email publisher`.
- PRs: descreva objetivo, como testar localmente, checklist (build green, testes, revisão).

Como rodar localmente rápido

```powershell
cd source/WebApi
dotnet run
```

Se você precisa rodar com Postgres/Redis/RabbitMQ local, use Docker Compose (não incluso neste repo) ou serviços em cloud de dev.

Checklist de segurança (resumo prático)
- Não commitar segredos
- Habilitar HTTPS e HSTS
- JWT secret guardado fora do repositório
- TLS para RabbitMQ/Redis/DB em produção
- Rate limiting e CORS restrito

Se quiser, eu posso:
- Gerar templates `appsettings.{env}.example.json` em outro formato
- Adicionar um `docker-compose.dev.yml` com Postgres/Redis/RabbitMQ para testes locais
## Getting Started

Para uso do template é preciso ter a seguinte SDK:
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (latest version)


## Tecnologias

* [ASP.NET Core 8](https://docs.microsoft.com/en-us/aspnet/core/introduction-to-aspnet-core)
* [Entity Framework Core 8](https://docs.microsoft.com/en-us/ef/core/)
* [MediatR](https://github.com/jbogard/MediatR)
* [AutoMapper](https://automapper.org/)
* [FluentValidation](https://fluentvalidation.net/)
* [NUnit](https://nunit.org/), [FluentAssertions](https://fluentassertions.com/), [Moq](https://github.com/moq) & [Respawn](https://github.com/jbogard/Respawn)
<!-- * [Angular 15](https://angular.io/) or [React 18](https://react.dev/) !-->
## Migrations

* **Adicionando:** dotnet ef migrations add Initial_Migration -p Infrastructure/ -s WebApi/ --output-dir Data/Migrations

* **Removendo:** dotnet ef migrations remove -p Infrastructure/ -s WebApi/

## Execução 

* **Iniciar (PowerShell):** docker-compose build --no-cache; docker-compose up