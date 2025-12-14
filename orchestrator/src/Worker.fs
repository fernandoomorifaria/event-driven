namespace Orchestrator

open System
open System.Threading
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open Types
open Thoth.Json.Net

type Worker(environment: Environment) =
    inherit BackgroundService()

    let complete (saga: Saga) =
        task {
            do! environment.UpdateSaga { saga with State = Completed }

            environment.Logger.LogInformation("Saga {id} completed", saga.SagaId)

            let key = saga.Order.OrderId.ToString()

            let event =
                { OrderId = saga.Order.OrderId
                  Status = Placed }

            do! environment.Publish "order-replies" key (Encode.orderEvent event |> Encode.toString 4)
        }

    let fail (saga: Saga) =
        task {
            do! environment.UpdateSaga { saga with State = Failed }

            environment.Logger.LogInformation("Saga {id} failed", saga.SagaId)

            let key = saga.Order.OrderId.ToString()

            let event =
                { OrderId = saga.Order.OrderId
                  Status = Cancelled }

            do! environment.Publish "order-replies" key (Encode.orderEvent event |> Encode.toString 4)
        }

    let processPayment (saga: Saga) =
        task {
            let key = saga.Order.OrderId.ToString()

            let command =
                ProcessPaymentCommand
                    { SagaId = saga.SagaId
                      OrderId = saga.Order.OrderId
                      CustomerId = saga.Order.CustomerId
                      Amount = saga.Order.Amount
                      Type = ProcessPayment }

            do! environment.Publish "payments" key (Encode.command command |> Encode.toString 4)
        }

    let releaseInventory (saga: Saga) =
        task {
            let key = saga.Order.OrderId.ToString()
            let order = saga.Order

            let command =
                ReleaseInventoryCommand
                    { SagaId = saga.SagaId
                      OrderId = order.OrderId
                      ProductId = order.ProductId
                      Type = ReleaseInventory }

            do! environment.Publish "inventory" key (Encode.command command |> Encode.toString 4)
        }

    let handleReply (reply: Reply) =
        task {
            environment.Logger.LogInformation "Getting saga from reply"

            let! saga = environment.QuerySaga reply.SagaId

            match saga with
            | None -> ()
            | Some saga ->
                match reply.Type with
                | InventoryReserved -> do! processPayment saga
                | OutOfStock -> do! fail saga
                | InventoryReleased -> do! fail saga
                | PaymentProcessed -> do! complete saga
                | InsufficientFunds -> do! releaseInventory saga
        }

    let startSaga (order: Order) =
        task {
            let sagaId = Guid.NewGuid()

            let saga =
                { SagaId = sagaId
                  State = Pending
                  Order = order }

            environment.Logger.LogInformation "Creating Saga"

            do! environment.CreateSaga saga

            let order = saga.Order

            let command =
                ReserveInventoryCommand
                    { SagaId = saga.SagaId
                      OrderId = order.OrderId
                      ProductId = order.ProductId
                      Type = ReserveInventory }

            let key = order.OrderId.ToString()

            environment.Logger.LogInformation("Starting Saga {sagaId}", saga.SagaId)

            do! environment.Publish "inventory" key (Encode.command command |> Encode.toString 4)
        }

    let handleMessage (topic: string) (message: string) =
        task {
            if topic = "order-requests" then
                let order = Decode.fromString Decode.order message

                match order with
                | Ok order ->
                    environment.Logger.LogInformation("Order {id} received", order.OrderId)

                    do! startSaga order
                | Error e -> environment.Logger.LogError e
            else
                let reply = Decode.fromString Decode.reply message

                match reply with
                | Ok reply ->
                    environment.Logger.LogInformation("Received {type} reply", reply.Type)

                    do! handleReply reply
                | Error e -> environment.Logger.LogError e
        }

    override _.ExecuteAsync(ct: CancellationToken) =
        task {
            while not ct.IsCancellationRequested do
                environment.Logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now)

                let result = environment.Consumer.Consume ct

                environment.Logger.LogInformation("Message received {message}", result.Message.Value)

                handleMessage result.Topic result.Message.Value |> ignore
        }
