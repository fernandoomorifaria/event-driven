module Encode

open Thoth.Json.Net
open Types

let order (order: Order) =
    Encode.object
        [ "orderId", Encode.guid order.OrderId
          "customerId", Encode.int order.CustomerId
          "productId", Encode.int order.ProductId
          "amount", Encode.decimal order.Amount ]

let commandType (commandType: CommandType) =
    match commandType with
    | ReserveInventory -> Encode.string "inventory.reserve"
    | ReleaseInventory -> Encode.string "inventory.release"
    | ProcessPayment -> Encode.string "payment.process"

let reserveInventory (command: ReserveInventoryCommand) =
    Encode.object
        [ "sagaId", Encode.guid command.SagaId
          "orderId", Encode.guid command.OrderId
          "type", commandType command.Type
          "productId", Encode.int command.ProductId ]

let releaseInventory (command: ReleaseInventoryCommand) =
    Encode.object
        [ "sagaId", Encode.guid command.SagaId
          "orderId", Encode.guid command.OrderId
          "type", commandType command.Type
          "productId", Encode.int command.ProductId ]

let processPayment (command: ProcessPaymentCommand) =
    Encode.object
        [ "sagaId", Encode.guid command.SagaId
          "orderId", Encode.guid command.OrderId
          "type", commandType command.Type
          "customerId", Encode.int command.CustomerId
          "amount", Encode.decimal command.Amount ]

let command (command: Command) =
    match command with
    | ReserveInventoryCommand command -> reserveInventory command
    | ReleaseInventoryCommand command -> releaseInventory command
    | ProcessPaymentCommand command -> processPayment command

let state (state: State) =
    match state with
    | Pending -> "Pending"
    | Completed -> "Completed"
    | Failed -> "Failed"

let saga (s: Saga) =
    Encode.object
        [ "sagaId", Encode.guid s.SagaId
          "state", Encode.string (state s.State)
          "order", order s.Order ]

let status (status: OrderStatus) =
    match status with
    | Placed -> Encode.string "placed"
    | Cancelled -> Encode.string "cancelled"

let orderEvent (event: OrderEvent) =
    Encode.object [ "orderId", Encode.guid event.OrderId; "status", status event.Status ]
