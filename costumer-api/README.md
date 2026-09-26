# Costumer Service

Microsserviço responsável pelo cadastro e consulta de clientes e pela publicação do evento ClienteCadastrado após a persistência do cadastro.

Repositório: https://github.com/Vitoria0/costumer-api

## Tecnologias

- .NET 10 e ASP.NET Core Web API com Controllers
- Entity Framework Core e SQL Server
- RabbitMQ com RabbitMQ.Client
- xUnit e Moq
- Swagger/OpenAPI com Swashbuckle

## Arquitetura

| Projeto | Responsabilidade |
| --- | --- |
| Costumer.Api | Endpoints HTTP, configuração e injeção de dependências. |
| Costumer.Application | Cadastro, consulta e mapeamento manual dos DTOs. |
| Costumer.Domain | Entidade, validações, evento e interfaces. |
| Costumer.Infrastructure | Persistência com EF Core, migrations e publicação no RabbitMQ. |
| Costumer.Tests | Testes unitários da entidade e do service com mocks. |

Api referencia Application e Infrastructure; Application referencia apenas Domain; Infrastructure referencia Application e Domain; Tests referencia Application e Domain. Domain não depende dos demais projetos nem de EF Core.

## Endpoints

### POST /api/v1/clientes

```json
{
  "cpf": "12345678901",
  "email": "maria@email.com",
  "nome": "Maria"
}
```

- **201 Created:** retorna `id`, `cpf`, `email` e `nome`, com `Location` apontando para a consulta por Id.
- **400 Bad Request:** entrada HTTP inválida ou falha de validação da entidade.
- **409 Conflict:** CPF ou e-mail já cadastrado, detectado pela consulta de duplicidade.

### GET /api/v1/clientes/{id}

Recebe um Guid. Retorna **200 OK** com `id`, `cpf`, `email` e `nome`, ou **404 Not Found** se não houver cliente. A consulta usa `AsNoTracking`.

## Regras de validação

- **CPF:** obrigatório, exatamente 11 caracteres de `0` a `9`, sem pontos, hífen ou máscara. Não valida dígitos verificadores neste desafio. Único no banco.
- **Email:** obrigatório, formato validado com `MailAddress`, recebe `Trim()` e `ToLowerInvariant()`. Único no banco.
- **Nome:** obrigatório, não aceita somente espaços e recebe `Trim()`.

## Banco de dados

SQL Server com Entity Framework Core. O banco `CostumerDb` é dedicado a este microsserviço. A migration `InitialCreate` cria `Costumers`:

| Coluna | Tipo | Restrições |
| --- | --- | --- |
| Id | uniqueidentifier | Chave primária, gerada pela aplicação |
| Cpf | varchar(11) | Obrigatória, índice único |
| Email | nvarchar(255) | Obrigatória, índice único |
| Nome | nvarchar(100) | Obrigatória |

O mapeamento fica em `CostumerConfiguration`, sem atributos de persistência no Domain.

## Mensageria

Após `SaveChangesAsync`, o service publica `ClienteCadastradoEvent` por meio de `IEventPublisher`. Payload real, com a capitalização atual do serializador:

```json
{
  "ClienteId": "aa45b087-3d60-456c-aa21-0493bb5be805"
}
```

O publisher declara a fila durável `cliente-cadastrado` no RabbitMQ. Publica pelo exchange padrão (`""`), com o nome da fila como routing key, mensagem persistente, `application/json` e confirmação do broker. Não é necessário binding adicional nesse exchange.

Conexão e canal são abertos e descartados a cada publicação. Não há consumer neste projeto.

## Como executar o projeto

Pré-requisitos: SDK .NET 10, SQL Server e RabbitMQ em execução e acessíveis.

1. Clone o repositório, substituindo o endereço abaixo pelo da sua cópia:

   ```powershell
   git clone <URL_DO_REPOSITORIO> credit-flow
   cd credit-flow/Costumer
   ```

   Todos os comandos seguintes partem da pasta `Costumer`, onde está `Costumer.slnx`.

2. Configure `ConnectionStrings:DefaultConnection` em `Costumer.Api/appsettings.Development.json` ou `Costumer.Api/appsettings.json`. A configuração atual usa `localhost,1436`, banco `CostumerDb` e usuário `sa`, sem senha gravada. Ajuste ao seu ambiente. Também pode sobrescrever pelo PowerShell:

   ```powershell
   $env:ConnectionStrings__DefaultConnection = "Server=localhost,1436;Database=CostumerDb;User Id=sa;Password=SUA_SENHA;TrustServerCertificate=True;"
   ```

   Não versione credenciais reais. Use o mesmo terminal para migrations e execução da API.

3. Configure a seção `RabbitMq` nos arquivos de configuração. Valores padrão de desenvolvimento local:

   ```json
   "RabbitMq": {
     "HostName": "localhost",
     "Port": 5672,
     "UserName": "guest",
     "Password": "guest"
   }
   ```

   Variáveis como `RabbitMq__Password` também sobrescrevem a configuração.

4. Restaure e compile:

   ```powershell
   dotnet restore Costumer.slnx
   dotnet build Costumer.slnx --no-restore
   ```

5. Confira a ferramenta EF Core:

   ```powershell
   dotnet ef --version
   ```

   Se não estiver instalada:

   ```powershell
   dotnet tool install --global dotnet-ef --version 10.0.12
   ```

   Se estiver em outra versão, use `dotnet tool update --global dotnet-ef --version 10.0.12`. Aplique a migration existente:

   ```powershell
   dotnet ef database update --project Costumer.Infrastructure --startup-project Costumer.Api
   ```

6. Execute a API:

   ```powershell
   dotnet run --project Costumer.Api --launch-profile http
   ```

   O perfil `http` configura explicitamente `http://localhost:5100` e ambiente `Development`. Swagger: `http://localhost:5100/swagger`. O endpoint `/health` verifica apenas que a API está ativa, sem verificar banco ou RabbitMQ.

## Como testar manualmente

Com banco, RabbitMQ e API disponíveis, abra o Swagger e use **Try it out**:

1. Execute o POST com o exemplo da seção Endpoints, usando CPF e e-mail ainda não cadastrados. Espere **201** e copie o `id`.
2. Execute GET com esse Id. Espere **200** e os mesmos dados. Um Guid inexistente deve retornar **404**.
3. Teste CPF duplicado (**409**), mantendo o CPF do cadastro e trocando o e-mail:

   ```json
   { "cpf": "12345678901", "email": "outro@email.com", "nome": "Maria" }
   ```

4. Teste e-mail duplicado (**409**), usando outro CPF:

   ```json
   { "cpf": "98765432109", "email": "maria@email.com", "nome": "Maria" }
   ```

5. Teste CPF inválido (**400**), com e-mail ainda não cadastrado:

   ```json
   { "cpf": "123.456.789-01", "email": "invalido@email.com", "nome": "Maria" }
   ```

   Para testar e-mail inválido ou nome vazio, use um CPF novo e altere o campo correspondente. As consultas de duplicidade acontecem antes da validação da entidade.

6. Se o plugin de gerenciamento do RabbitMQ estiver habilitado, abra seu painel (normalmente `http://localhost:15672`, conforme a instalação). Em **Queues**, abra `cliente-cadastrado` no virtual host `/`. Use **Get messages** com requeue habilitado para observar a mensagem sem removê-la definitivamente. Confira `ClienteId` igual ao Id do POST, `content_type: application/json` e `delivery_mode: 2`. Não é necessário criar consumer.

## Como executar testes

Na pasta `Costumer`:

```powershell
dotnet test Costumer.slnx
```

Ou somente o projeto de testes:

```powershell
dotnet test Costumer.Tests/Costumer.Tests.csproj
```

Os testes cobrem entidade, validações, normalização, cadastro, duplicidade, consulta e publicação somente após a persistência. Verificam também que falha de persistência impede a publicação. Repository e publisher usam mocks com Moq; não acessam SQL Server ou RabbitMQ.

## Decisões técnicas

- Banco dedicado ao microsserviço; SQL Server oferece persistência relacional e índices únicos.
- Domain sem EF Core; mapeamento de banco na Infrastructure.
- Repository abstrai persistência; service coordena cadastro, consulta e publicação.
- RabbitMQ abstraído por `IEventPublisher`, sem acoplamento da Application ao broker.
- Guid gerado pela entidade e CPF armazenado como string, preservando zeros à esquerda.
- DTOs mapeados manualmente.