# Card Service

Microsserviço responsável pela emissão e consulta de cartões de crédito após aprovação de uma proposta.

Repositório: https://github.com/Vitoria0/card-api

## Tecnologias

- .NET 10 e ASP.NET Core Web API com Controllers
- Entity Framework Core e SQL Server
- RabbitMQ.Client
- xUnit e Moq
- Swagger/OpenAPI com Swashbuckle

## Arquitetura

| Projeto | Responsabilidade |
| --- | --- |
| Card.Api | GET de cartões, configuração, Swagger e injeção de dependências. |
| Card.Application | DTOs, interface do service, validação do evento e emissão dos cartões. |
| Card.Domain | Entidade Card, contrato PropostaGeradaEvent e interface do repository. Sem EF Core, ASP.NET Core ou RabbitMQ. |
| Card.Infrastructure | EF Core, SQL Server, repository, consumer RabbitMQ, retry e DLQ. |
| Card.Tests | Testes unitários da entidade e do service com mocks. |

Referências: Api → Application e Infrastructure; Application → Domain; Infrastructure → Domain e Application; Tests → Domain e Application. A referência da Infrastructure à Application permite ao consumer chamar `ICardService`, seguindo a organização do Credit. Application usa apenas abstrações de logging além do Domain.

## Fluxo

Credit → PropostaGerada → RabbitMQ → PropostaGeradaConsumer → CardService → validação → criação dos cartões → repository → SQL Server → ACK.

O consumer abre um escopo de DI por mensagem. Os cartões da emissão são persistidos em um único `SaveChangesAsync`. Propostas não aprovadas e eventos válidos de clientes que já possuem cartões retornam normalmente e recebem ACK.

## Regras de emissão

| Score | Resultado |
| --- | --- |
| 0–100 | Nenhum cartão; status aprovado nessa faixa é inválido. |
| 101–500 | Exatamente 1 cartão de R$ 1.000. |
| 501–1000 | Exatamente 2 cartões de R$ 5.000 cada, total R$ 10.000. |

`ClienteId` deve ser diferente de Guid.Empty. Se `Status` não for `Aprovado`, o evento é ignorado. Para propostas aprovadas, score, quantidade e limite precisam ser consistentes. Dados incoerentes lançam `ArgumentException` e seguem retry/DLQ. A entidade também exige limite positivo, gera o Guid e registra `CriadoEm` em UTC.

## Endpoint

`GET /api/v1/cartoes/{clienteId}`

- **200 OK:** coleção de cartões com `id`, `clienteId`, `limite` e `criadoEm`.
- **404 Not Found:** nenhum cartão encontrado para o cliente.

Não há POST; cartões são emitidos por mensageria.

## Mensageria

Evento consumido: `PropostaGeradaEvent`.

```json
{
  "ClienteId": "c414386e-08d5-4a87-97dc-86cdfd389823",
  "Score": 700,
  "Status": "Aprovado",
  "LimitePorCartao": 5000,
  "QuantidadeCartoes": 2
}
```

O contrato coincide com o Credit. A desserialização também aceita propriedades em camelCase.

| Fila | Finalidade |
| --- | --- |
| `proposta-gerada` | Fila principal já usada pelo publisher do Credit. |
| `card.proposta-gerada.retry` | Aguarda 5 segundos antes de voltar à principal. |
| `card.proposta-gerada.dlq` | Preserva mensagens que esgotaram os retries. |

Todas são duráveis. O exchange padrão (`""`) roteia pelo nome da fila; não há exchange customizado nem binding adicional. A principal mantém o nome existente para funcionar sem alterar o Credit. Filas são declaradas quando a API inicia com sucesso a conexão ao RabbitMQ.

## Retry

O header `x-retry-count` começa em 0 quando ausente. Em falha, se for menor que 3, o consumer incrementa o valor e republica o payload na retry queue como mensagem persistente. Somente após confirmação do broker envia ACK da original.

A retry queue usa `x-message-ttl = 5000`, `x-dead-letter-exchange = ""` e `x-dead-letter-routing-key = proposta-gerada`. O broker devolve a mensagem à principal após o TTL, sem bloquear o consumer.

Seguindo a condição `retryCount < 3`, há **até 3 reenvios além da execução inicial: 4 execuções no total**, como no Credit. O contador viaja no header, não em memória. O `Task.Delay` infinito do hosted service apenas mantém seu ciclo de vida; não retém mensagens para retry.

## DLQ

Em falha com `x-retry-count >= 3`, o payload original e os headers relevantes são publicados em `card.proposta-gerada.dlq`. A original recebe ACK após a confirmação. Não há consumo ou reprocessamento automático da DLQ.

Se a republicação falhar, a aplicação encerra sem confirmar a original, permitindo que o broker a disponibilize novamente. Não há retry de conexão automático.

## Idempotência

O service verifica `ExistsByClienteIdAsync` antes da emissão. Se já houver cartões, registra que o evento foi ignorado por idempotência e retorna. O consumer envia ACK, sem novos cartões, retry ou DLQ.

A estratégia assume uma única emissão por proposta/cliente neste desafio e processamento sequencial em uma única instância. Não há índice UNIQUE em ClienteId, pois são permitidos dois cartões. Portanto, essa consulta prévia **não garante exclusão mútua entre instâncias concorrentes**.

## Banco de dados

SQL Server com banco separado `CardDb`. A configuração local usa a instância da porta 1437, compartilhando apenas o servidor com Credit; as tabelas estão em bancos diferentes.

Migration: `InitialCreate`, em `Card.Infrastructure/Data/Migrations`.

| Coluna | Tipo | Regra |
| --- | --- | --- |
| Id | uniqueidentifier | Chave primária gerada pela aplicação. |
| ClienteId | uniqueidentifier | Obrigatório, índice não único. |
| Limite | decimal(18,2) | Obrigatório. |
| CriadoEm | datetime2 | Obrigatório, tratado como UTC na leitura e escrita. |

## Como executar

Pré-requisitos: SDK .NET 10, SQL Server acessível, RabbitMQ acessível e ferramenta `dotnet-ef` 10.0.12 para migrations.

1. Clone seu repositório e entre em `Card`:

   ```powershell
   git clone <URL_DO_REPOSITORIO> credit-flow
   cd credit-flow/Card
   ```

2. Configure a conexão no mesmo terminal usado para migration e execução:

   ```powershell
   $env:ConnectionStrings__DefaultConnection = "Server=localhost,1437;Database=CardDb;User Id=sa;Password=SUA_SENHA;TrustServerCertificate=True;"
   ```

   Ajuste servidor e credenciais. `appsettings.json` contém uma conexão sem senha. Não versione credenciais reais. Você também pode configurar `ConnectionStrings:DefaultConnection` localmente em `appsettings.Development.json`.

3. Confira `RabbitMq` em `Card.Api/appsettings.json`:

   ```json
   "RabbitMq": {
     "HostName": "localhost",
     "Port": 5672,
     "UserName": "guest",
     "Password": "guest"
   }
   ```

   Credenciais locais podem ser sobrescritas por `RabbitMq__UserName` e `RabbitMq__Password`.

4. Restaure e compile:

   ```powershell
   dotnet restore Card.slnx
   dotnet build Card.slnx
   ```

5. Se necessário, instale a ferramenta EF Core e aplique a migration existente:

   ```powershell
   dotnet tool install --global dotnet-ef --version 10.0.12
   dotnet ef database update --project Card.Infrastructure --startup-project Card.Api
   ```

   Se a ferramenta já existir, não repita a instalação. O comando usado para gerar a migration foi `dotnet ef migrations add InitialCreate --project Card.Infrastructure --startup-project Card.Api --output-dir Data/Migrations`; não é necessário recriá-la.

6. Execute:

   ```powershell
   dotnet run --project Card.Api --launch-profile http
   ```

   O perfil configura `http://localhost:5300`. Swagger em `http://localhost:5300/swagger`, no ambiente Development. A API inicia o consumer automaticamente; RabbitMQ deve estar disponível.

## Como testar manualmente

1. Inicie SQL Server e RabbitMQ, aplique a migration e execute Card.Api com as credenciais configuradas.
2. No painel de gerenciamento do RabbitMQ, se habilitado (normalmente `http://localhost:15672`), abra a fila `proposta-gerada` no virtual host `/`.
3. Em Publish message, publique o JSON de exemplo com ClienteId novo, `content_type: application/json` e `delivery_mode: 2`. Também é possível produzir a mensagem pelo fluxo normal do Credit.
4. Consulte `GET http://localhost:5300/api/v1/cartoes/{clienteId}`. Para score 700, espere 200 com dois cartões de 5000. Confira também os registros de `CardDb.dbo.Cards`.
5. Republique o mesmo evento. Os mesmos dois cartões devem permanecer, sem mensagem nova em retry/DLQ. O log deve informar idempotência.
6. Publique um evento negado para outro cliente. Nenhum cartão deve ser criado; GET retorna 404.
7. Para simular erro de validação, publique com ClienteId novo, score 700, status Aprovado, limite 1000 e quantidade 2. Observe os logs e a retry queue: os headers percorrem 1, 2 e 3, com aproximadamente 5 segundos entre retornos.
8. Após a quarta falha total, confira `card.proposta-gerada.dlq`, payload preservado e header `x-retry-count: 3`. Use Get messages com requeue para observar sem remover a mensagem. Não há reprocessamento automático.

## Como executar testes

Na pasta `Card`:

```powershell
dotnet test Card.slnx
```

Os testes usam xUnit e Moq, sem SQL Server ou RabbitMQ. Cobrem a entidade, limites de score, consistência do evento, propostas negadas, idempotência e mapeamento da consulta. Retry/DLQ ficam no consumer acoplado ao RabbitMQ.Client e não possuem testes unitários próprios; não foram adicionadas abstrações só para testar essa parte.

## Decisões técnicas

- Banco próprio e Domain sem EF Core.
- Repository para persistência, service para emissão e mapeamento manual de DTOs.
- RabbitMQ com ACK manual e um evento em processamento por consumer (prefetch 1).
- Retry nativo com TTL + DLX e DLQ durável.
- Idempotência simples por ClienteId, sem tabela adicional ou cache.
- Sem Outbox e sem bibliotecas de resiliência, mantendo o escopo do desafio.
