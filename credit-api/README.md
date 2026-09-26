# Credit Service

Microsserviço responsável por gerar e consultar propostas de crédito a partir do evento ClienteCadastrado. Propostas aprovadas originam o evento PropostaGerada, utilizado pelo microsserviço Card.

Repositório: https://github.com/Vitoria0/credit-api

## Tecnologias

- .NET 10 e ASP.NET Core Web API com Controllers
- Entity Framework Core e SQL Server
- RabbitMQ.Client
- Swagger/OpenAPI com Swashbuckle
- xUnit e Moq

## Arquitetura

| Projeto | Responsabilidade |
| --- | --- |
| Credit.Api | Consulta HTTP, Swagger, configuração e injeção de dependências. |
| Credit.Application | Service de propostas, DTOs e geração simples de score. |
| Credit.Domain | Entidade, regras de score, contratos de eventos e interfaces. |
| Credit.Infrastructure | EF Core, repositories, migrations, consumer e publisher RabbitMQ. |
| Credit.Tests | Testes unitários da entidade, do service e do gerador de score. |

Referências: Api → Application e Infrastructure; Application → Domain; Infrastructure → Domain e Application; Tests → Domain e Application. A referência da Infrastructure à Application permite ao consumer chamar o service. Domain não depende de EF Core, ASP.NET Core ou RabbitMQ.

## Fluxo

Costumer → ClienteCadastrado → RabbitMQ → ClienteCadastradoConsumer → gera score → CreditProposalService → SQL Server → se aprovada, publica PropostaGerada → Card.

O consumer cria um escopo de DI por mensagem. O service verifica duplicidade por ClienteId, cria a entidade e persiste a proposta antes de publicar. Propostas negadas também são persistidas, mas não publicam evento. A mensagem recebida é confirmada após o service concluir.

## Regras de crédito

O `RandomScoreGenerator` gera um inteiro de 0 a 1000, inclusive. Não há consulta a um serviço externo de análise de crédito.

| Score | Status | Limite por cartão | Quantidade de cartões |
| --- | --- | --- | --- |
| 0–100 | Negado | R$ 0 | 0 |
| 101–500 | Aprovado | R$ 1.000 | 1 |
| 501–1000 | Aprovado | R$ 5.000 | 2 |

A entidade `CreditProposal` define esses valores. ClienteId não pode ser Guid.Empty e score fora de 0–1000 é rejeitado. O Id da proposta é gerado pela própria entidade.

## Endpoint

`GET /api/v1/propostas/{clienteId}`

- **200 OK:** proposta encontrada.
- **404 Not Found:** nenhuma proposta para o cliente.

Exemplo de resposta:

```json
{
  "id": "ab5933b2-77c9-4139-a80a-f886824dd1eb",
  "clienteId": "c414386e-08d5-4a87-97dc-86cdfd389823",
  "score": 700,
  "status": "Aprovado",
  "limitePorCartao": 5000,
  "quantidadeCartoes": 2
}
```

Não há POST de proposta. A criação ocorre pelo consumo de mensagens.

## Mensageria

### Evento recebido: ClienteCadastradoEvent

```json
{
  "ClienteId": "c414386e-08d5-4a87-97dc-86cdfd389823"
}
```

Use a propriedade `ClienteId` com essa capitalização, compatível com o publisher do Costumer e com a desserialização atual do consumer.

### Evento publicado: PropostaGeradaEvent

Somente para propostas aprovadas, depois da persistência:

```json
{
  "ClienteId": "c414386e-08d5-4a87-97dc-86cdfd389823",
  "Score": 700,
  "Status": "Aprovado",
  "LimitePorCartao": 5000,
  "QuantidadeCartoes": 2
}
```

| Fila | Finalidade |
| --- | --- |
| `cliente-cadastrado` | Entrada publicada pelo Costumer. |
| `cliente-cadastrado.retry` | Atraso antes de uma nova tentativa. |
| `cliente-cadastrado.dlq` | Mensagens que esgotaram os retries. |
| `proposta-gerada` | Saída para o Card, declarada pelo publisher ao publicar. |

As filas são duráveis e usam o exchange padrão (`""`), com o nome da fila como routing key. As mensagens republicadas e os eventos de saída são persistentes. O evento de saída usa `application/json`.

## Retry e DLQ

Falhas reais seguem este fluxo:

```text
cliente-cadastrado
  → falha: incrementa x-retry-count e publica em cliente-cadastrado.retry
  → TTL de 5 segundos
  → dead-letter exchange padrão devolve à cliente-cadastrado
  → falha com contador 3: publica em cliente-cadastrado.dlq
```

O header `x-retry-count` começa em 0 quando ausente. A condição `retryCount < 3` permite **3 reenvios além da execução inicial**, totalizando até **4 execuções**. O contador viaja na mensagem; não é armazenado em memória.

A fila de retry usa `x-message-ttl = 5000`, `x-dead-letter-exchange = ""` e `x-dead-letter-routing-key = cliente-cadastrado`. Não há espera bloqueando o consumer: o atraso é feito pelo broker.

O payload original e os headers relevantes são preservados. A mensagem original só recebe ACK após confirmação da republicação para retry ou DLQ. Se esse encaminhamento falhar, a aplicação encerra sem ACK. A DLQ não é consumida automaticamente.

## Idempotência

ClienteId é a chave natural. O service verifica `ExistsByClienteIdAsync` antes de criar a proposta e lança `ProposalAlreadyExistsException` se ela já existir.

O consumer trata essa exceção separadamente: registra que a proposta já foi processada e envia ACK, sem nova persistência, publicação, retry ou DLQ.

O índice UNIQUE `IX_CreditProposals_ClienteId` impede propostas duplicadas em processamento simultâneo. O repository converte apenas erros SQL Server 2601/2627 que identificam esse índice e a tabela `dbo.CreditProposals` na mesma exceção. Outros erros de banco continuam sendo falhas reais.

## Banco de dados

Banco próprio `CreditDb`, em SQL Server, com Entity Framework Core. A migration `20260925173742_InitialCreate` cria a tabela `CreditProposals`:

| Coluna | Tipo | Regra |
| --- | --- | --- |
| Id | uniqueidentifier | Chave primária gerada pela aplicação. |
| ClienteId | uniqueidentifier | Obrigatório, índice UNIQUE. |
| Score | int | Obrigatório. |
| Status | nvarchar(20) | Obrigatório. |
| LimitePorCartao | decimal(18,2) | Obrigatório. |
| QuantidadeCartoes | int | Obrigatório. |

O mapeamento fica em `CreditProposalConfiguration`. As consultas usam `AsNoTracking`.

## Como executar

Pré-requisitos: SDK .NET 10, SQL Server e RabbitMQ acessíveis. A ferramenta `dotnet-ef` 10.0.12 é necessária para aplicar migrations.

1. Na raiz do repositório, entre na pasta do serviço:

   ```powershell
   cd Credit
   ```

2. Configure a conexão no terminal em que executará os comandos:

   ```powershell
   $env:ConnectionStrings__DefaultConnection = "Server=localhost,1437;Database=CreditDb;User Id=sa;Password=SUA_SENHA;TrustServerCertificate=True;"
   ```

   Ajuste servidor e credenciais ao seu ambiente. `Credit.Api/appsettings.json` contém a conexão local sem senha. Não versione credenciais reais. Também é possível ajustar `ConnectionStrings:DefaultConnection` em `appsettings.Development.json`.

3. Confira a seção `RabbitMq`:

   ```json
   "RabbitMq": {
     "HostName": "localhost",
     "Port": 5672,
     "UserName": "guest",
     "Password": "guest"
   }
   ```

   Os valores são de desenvolvimento local. Variáveis como `RabbitMq__HostName` e `RabbitMq__Password` podem sobrescrevê-los.

4. Restaure e compile:

   ```powershell
   dotnet restore Credit.slnx
   dotnet build Credit.slnx
   ```

5. Se ainda não tiver a ferramenta, instale-a:

   ```powershell
   dotnet tool install --global dotnet-ef --version 10.0.12
   ```

   Aplique a migration existente:

   ```powershell
   dotnet ef database update --project Credit.Infrastructure --startup-project Credit.Api
   ```

6. Execute a API:

   ```powershell
   dotnet run --project Credit.Api --launch-profile http
   ```

   O perfil configura explicitamente `http://localhost:5200` e ambiente Development. Swagger: `http://localhost:5200/swagger`. O consumer inicia junto com a API; RabbitMQ deve estar disponível.

## Como testar manualmente

1. Inicie SQL Server e RabbitMQ, aplique a migration e execute Credit.Api.
2. Cadastre um cliente no Costumer ou publique o JSON de ClienteCadastradoEvent na fila `cliente-cadastrado`, usando um ClienteId novo. No painel RabbitMQ, se habilitado, normalmente disponível em `http://localhost:15672`, use o virtual host `/` e a opção Publish message, com `content_type: application/json` e `delivery_mode: 2`.
3. Consulte `GET http://localhost:5200/api/v1/propostas/{clienteId}`. Confira o score e os valores correspondentes à faixa. Um cliente inexistente retorna 404.
4. Se aprovada, confira PropostaGeradaEvent em `proposta-gerada`. Se Card estiver em execução, ele poderá consumir a mensagem imediatamente; nesse caso, confira também o resultado no Card. Propostas negadas não publicam evento.
5. Republique o mesmo ClienteId: deve continuar existindo uma única proposta e o log deve indicar mensagem ignorada por idempotência, sem novo evento de saída.
6. Para provocar uma falha de validação sem alterar o banco, publique `{"ClienteId":"00000000-0000-0000-0000-000000000000"}`. Observe os retries e, após aproximadamente 15 segundos mais o tempo de processamento, a mensagem na DLQ com `x-retry-count: 3`.
7. Ao inspecionar a DLQ por Get messages, habilite requeue para preservar a mensagem. Não há reprocessamento automático.

Como o score é aleatório, não há garantia de aprovação em um cadastro específico. As faixas são verificadas de forma determinística nos testes unitários.

## Como executar testes

Na pasta `Credit`:

```powershell
dotnet test Credit.slnx
```

Ou somente o projeto de testes:

```powershell
dotnet test Credit.Tests/Credit.Tests.csproj
```

Os testes cobrem limites das faixas de score, entradas inválidas, persistência, mapeamento, consulta, duplicidade, publicação somente de propostas aprovadas após persistência e intervalo do gerador de score. Repository e publisher são mockados com Moq; não há acesso a banco ou RabbitMQ.

Retry/DLQ estão acoplados ao consumer RabbitMQ e não possuem testes unitários próprios. Não há testes automatizados de integração.

## Decisões técnicas

- Banco separado por microsserviço e Domain sem dependências de infraestrutura.
- Repository para persistência; service para coordenar criação, consulta e publicação.
- Regras de score concentradas na entidade e geração simples por `Random.Shared`.
- DTOs mapeados manualmente e RabbitMQ abstraído por `IEventPublisher`.
- ACK manual, prefetch 1 e escopo de DI por mensagem.
- Retry com TTL + DLX e idempotência por ClienteId, sem tabela auxiliar.
