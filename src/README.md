# Patch — Consumer RabbitMQ + Inbox

Patch para aplicar na raiz do repositório `GBastos.Comven_VehicleBilling`, preservando a pasta `src`.

## Arquivos

- `GBastos.Comven_VehicleBilling.Infrastructure/Inbox/InboxRepository.cs`
- `GBastos.Comven_VehicleBilling.Infrastructure/Mensageria/RabbitMqPublisher.cs`
- `GBastos.Comven_VehicleBilling.Worker/WorkerService/Program.cs`
- `GBastos.Comven_VehicleBilling.Worker/WorkerService/RabbitMqConsumer.cs`
- `GBastos.Comven_VehicleBilling.Worker/WorkerService/Events/OperationCreatedEvent.cs`
- `GBastos.Comven_VehicleBilling.Worker/WorkerService/Events/OperationCreatedHandler.cs`
- `GBastos.Comven_VehicleBilling.Worker/WorkerService/Events/PermanentMessageException.cs`

## Comportamento implementado

1. `RabbitMqConsumer` registrado como Hosted Service no Worker.
2. `autoAck: false` e `BasicQos(prefetchCount: 1)` mantidos.
3. Inserção atômica da Inbox via `INSERT ... ON CONFLICT (id, consumer) DO NOTHING`.
4. A chave primária composta já existente (`id`, `consumer`) protege concorrência sem migration.
5. Transação PostgreSQL engloba registro da Inbox e execução/validação do handler.
6. `ACK` somente depois do `COMMIT`.
7. Duplicatas confirmadas na Inbox recebem `ACK` sem reprocessamento.
8. Falhas transitórias recebem `NACK` com `requeue: true`.
9. Payload inválido/tipo desconhecido e mensagem sem MessageId válido recebem `NACK` com `requeue: false`.
10. Removidos `PasswordLength`, `PasswordFingerprint`, `RABBITMQ_DIAG` e cálculo SHA-256 da senha no Publisher.

## Limite funcional explícito

O evento `OperationCreated.v1` atualmente é uma notificação de uma operação que a API já persistiu. O handler valida o contrato e registra o consumo durável pela Inbox; ele **não executa novamente cobrança, débito ou faturamento**, evitando repetir efeitos financeiros. Quando surgir uma ação de negócio downstream, ela deverá ser adicionada ao handler e persistida na mesma transação.

A fila existente não possui configuração de Dead Letter Exchange (DLX). Por isso, mensagens permanentemente inválidas rejeitadas com `requeue: false` serão descartadas pelo RabbitMQ. Para retê-las em DLQ será necessário configurar a fila com DLX (o que exige tratar a declaração da fila existente e sua configuração no broker).

## Aplicação

Extraia este ZIP na raiz do repositório local com a opção de substituir os arquivos existentes. Os caminhos já começam por `src/`, correspondendo à estrutura local do projeto. Não há alteração de banco nem migration.

Depois, compile e execute os testes no Windows com o SDK .NET 9 instalado. Este ambiente de preparação não possui `dotnet`, então a compilação e os testes automatizados não puderam ser executados aqui.
