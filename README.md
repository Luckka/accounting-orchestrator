# Mini Roteiro Contábil — C# + AWS (Free Tier)

Projeto exemplo demonstrando arquitetura segura e seguindo SOLID usando AWS (S3, SQS, Lambda, DynamoDB, EventBridge).

Conteúdo gerado:
- Código C# para duas Lambdas: `Ingest` (HTTP) e `Processor` (SQS trigger)
- Biblioteca comum com modelos, serviço de "explosão contábil" e repositório DynamoDB
- Políticas IAM de menor privilégio (em `infra/policies`)
- Script PowerShell de build/deploy (`scripts/deploy.ps1`)

Visão geral:
- Ingest online: API Gateway -> Ingest Lambda -> envia mensagem para SQS
- Ingest batch: upload no S3 -> notificação para SQS
- Worker: Processor Lambda acionado por SQS, processa payload (ou baixa de S3), executa explosão contábil e grava entries no DynamoDB
- Scheduler: EventBridge (regra cron) envia mensagem para SQS (configurar no Console)

Pré-requisitos locais:
- .NET 8 SDK (recomendado; projetos atualizados para net8.0)
- AWS CLI configurado (credenciais)
- dotnet tool: `Amazon.Lambda.Tools` (opcional, usado no script)

Como usar (resumo):
1. Leia `infra/policies/lambda-processing-policy.json` e crie uma IAM Role baseada nela (Console IAM).
2. Crie S3 bucket (block public access + SSE), SQS queue (com DLQ), DynamoDB table (AccountingEntries).
3. Ajuste variáveis de ambiente nas Lambdas: `SQS_URL`, `DDB_TABLE`.
4. Build: `.\scripts\deploy.ps1 build`
5. Deploy: recomendo `dotnet lambda deploy-function` ou utilizar o Console AWS -> criar função .NET e fazer upload do ZIP gerado.

Idempotência e GSI (importante)
 - O repositório Dynamo tem checagem de idempotência por `BatchId`. Para isso, a tabela `AccountingEntries` precisa ter um Global Secondary Index com:
   - Index name: `BatchId-index`
   - Partition key: `BatchId` (String)
   - Projection: Keys only (ou All)

Ao criar a tabela via Console DynamoDB: em "Indexes" -> "Create index" defina `BatchId` como Partition key com o nome `BatchId-index`.

Testes
 - Projetos de teste estão em `src/Accounting.Tests`. Rode `dotnet test` para executar os testes unitários locais.

Samples
 - `samples/invoice.json` — exemplo de payload tipo invoice.
 - `samples/array.json` — exemplo de payload como array de lançamentos.

Deploy via AWS SAM (recomendado)
1. Instale o AWS SAM CLI: https://docs.aws.amazon.com/serverless-application-model/latest/developerguide/serverless-sam-cli-install.html
2. No root do projeto execute:
   - `sam build` (o SAM irá construir os projetos .NET)
   - `sam deploy --guided` (forneça um stack name e aceite criar recursos)
3. Após deploy, anote:
   - API endpoint (Output ApiEndpoint) para POST /ingest
   - S3 bucket name (Output S3BucketName) para uploads batch
   - SQS queue URL (Output SQSQueueUrl)
   - DynamoDB table (Output DynamoDBTableName) — criar GSI BatchId-index se necessário (o template já cria o índice)

Notas:
- O SAM template `template.yaml` cria resources com nomes que contêm seu AccountId/Region para evitar colisões.
- Verifique os logs no CloudWatch para depurar (cada Lambda tem role básico para CloudWatch).

