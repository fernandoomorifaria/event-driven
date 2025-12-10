module Orders.App

open System
open System.Text.Json
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.Hosting
open Confluent.Kafka
open Giraffe
open Types

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

        ProducerBuilder<string, string>(config).Build()

    let private createStartSaga (configuration: IConfiguration) (producer: IProducer<string, string>) =
        let topic = configuration.["Kafka:Topics:Orchestrator"]

        fun (command: CreateOrderCommand) ->
            task {
                let key = command.OrderId.ToString()
                let json = JsonSerializer.Serialize command

                let message = Message<string, string>(Key = key, Value = json)

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
