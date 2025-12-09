namespace Orchestrator

open System
open System.Collections.Generic
open System.Linq
open System.Threading
open System.Threading.Tasks
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open System.Text.Json
open Confluent.Kafka

type Order =
    { OrderId: Guid
      CustomerId: int
      ProductId: int
      Amount: decimal }

(* TODO: Add state field *)
(* TODO: Use ADT *)
type SagaMessage =
    { SagaId: Guid
      Type: string
      Order: Order }

type Worker(producer: IProducer<Null, string>, consumer: IConsumer<Null, string>, logger: ILogger<Worker>) =
    inherit BackgroundService()

    let publish (topic: string) (message: string) =
        task {
            let! _ = producer.ProduceAsync(topic, Message<Null, string>(Value = message))

            ()
        }

    let handleOrder (message: string) =
        task {
            let order = JsonSerializer.Deserialize<Order> message

            logger.LogInformation("Order {id} received", order.OrderId)

            (* TODO: Start SAGA state and store in database *)
            let sagaId = Guid.NewGuid()

            let command =
                { SagaId = sagaId
                  Type = "ProcessPayment"
                  Order = order }

            do! publish "payments" (JsonSerializer.Serialize command)
        }

    let handleMessage (topic: string) (message: string) =
        task {
            (* TODO: JsonConverter to ADT? *)
            match topic with
            | "orders" -> do! handleOrder message
            | _ -> ()
        }

    override _.ExecuteAsync(ct: CancellationToken) =
        task {
            while not ct.IsCancellationRequested do
                logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now)

                let result = consumer.Consume(ct)

                handleMessage result.Topic result.Message.Value |> ignore
        }
