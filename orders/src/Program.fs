module Orders.App

open System
open System.Text.Json
open System.Threading.Tasks
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.Hosting
open Confluent.Kafka
open Giraffe

type CreateOrderCommand =
    { OrderId: Guid
      CustomerId: int
      ProductId: int
      Amount: decimal }

type StartSaga = CreateOrderCommand -> Task<unit>

type Environment = { StartSaga: StartSaga }

type CreateOrderRequest =
    { CustomerId: int
      ProductId: int
      Amount: decimal }

let createOrderHandler (startSaga: StartSaga) : HttpHandler =
    fun (next: HttpFunc) (ctx: HttpContext) ->
        task {
            let! request = ctx.BindJsonAsync<CreateOrderRequest>()

            let orderId = Guid.NewGuid()

            let command =
                { OrderId = orderId
                  CustomerId = request.CustomerId
                  ProductId = request.ProductId
                  Amount = request.Amount }

            do! startSaga command

            ctx.SetStatusCode 201

            return! next ctx
        }

let webApp (environment: Environment) =
    choose
        [ POST >=> choose [ route "/order" >=> createOrderHandler environment.StartSaga ]
          setStatusCode 404 >=> text "Not Found" ]

module CompositionRoot =
    let private createProducer (configuration: IConfiguration) =
        let server = configuration.["Kafka:BootstrapServer"]
        let config = ProducerConfig(BootstrapServers = server)

        ProducerBuilder<Null, string>(config).Build()

    let private createStartSaga (configuration: IConfiguration) (producer: IProducer<Null, string>) =
        let topic = configuration.["Kafka:Topics:Orchestrator"]

        fun (command: CreateOrderCommand) ->
            task {
                let json = JsonSerializer.Serialize command

                let message = Message<Null, string>(Value = json)

                let! _ = producer.ProduceAsync(topic, message)

                ()
            }

    let compose (configuration: IConfiguration) : Environment =
        let producer = createProducer configuration
        let StartSaga = createStartSaga configuration producer

        { StartSaga = StartSaga }

[<EntryPoint>]
let main args =
    let builder = WebApplication.CreateBuilder args

    builder.Services.AddGiraffe() |> ignore

    let app = builder.Build()

    let environment = CompositionRoot.compose builder.Configuration

    app.UseGiraffe(webApp environment)
    app.Run()

    0
