module Types

open System
open System.Data
open System.Threading.Tasks
open Microsoft.Extensions.Logging
open Confluent.Kafka

type Order =
    { OrderId: Guid
      CustomerId: int
      ProductId: int
      Amount: decimal }

type State =
    | Pending
    | Completed
    | Failed

type Saga =
    { SagaId: Guid
      State: State
      Order: Order }

type SagaEntity =
    { Id: int
      SagaId: Guid
      State: string
      Order: string
      CreatedAt: DateTime
      LastUpdatedAt: DateTime }

type ReplyType =
    | InventoryReserved
    | OutOfStock
    | InventoryReleased
    | PaymentProcessed
    | InsufficientFunds

type Reply =
    { SagaId: Guid
      OrderId: Guid
      Type: ReplyType }

type CommandType =
    | ReserveInventory
    | ReleaseInventory
    | ProcessPayment

type ReserveInventoryCommand =
    { SagaId: Guid
      OrderId: Guid
      Type: CommandType
      ProductId: int }

type ReleaseInventoryCommand =
    { SagaId: Guid
      OrderId: Guid
      Type: CommandType
      ProductId: int }

type ProcessPaymentCommand =
    { SagaId: Guid
      OrderId: Guid
      Type: CommandType
      CustomerId: int
      Amount: decimal }

type Command =
    | ReserveInventoryCommand of ReserveInventoryCommand
    | ReleaseInventoryCommand of ReleaseInventoryCommand
    | ProcessPaymentCommand of ProcessPaymentCommand

type OrderStatus =
    | Placed
    | Cancelled

type OrderEvent = { OrderId: Guid; Status: OrderStatus }

type QuerySaga = Guid -> Task<Saga option>

type CreateSaga = Saga -> Task<unit>

type UpdateSaga = Saga -> Task<unit>

type Environment =
    { Publish: string -> string -> string -> Task<unit>
      Consumer: IConsumer<string, string>
      QuerySaga: QuerySaga
      CreateSaga: CreateSaga
      UpdateSaga: UpdateSaga
      Logger: ILogger }
