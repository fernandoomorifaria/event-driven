module Orders.App

open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Confluent.Kafka
open Giraffe
open Npgsql
open Database
open Handlers
open Thoth.Json.Net
open Types

module CompositionRoot =
    let private createProducer (configuration: IConfiguration) =
        let server = configuration.["Kafka:BootstrapServer"]
        let config = ProducerConfig(BootstrapServers = server)

        ProducerBuilder<string, string>(config).Build()

    let private createConsumer (configuration: IConfiguration) =
        let server = configuration.["Kafka:BootstrapServer"]
        let config = ConsumerConfig(BootstrapServers = server, GroupId = "order-consumer")

        let consumer = ConsumerBuilder<string, string>(config).Build()

        consumer.Subscribe [| "order-replies" |]

        consumer

    let private createStartSaga (producer: IProducer<string, string>) =
        fun (command: CreateOrderCommand) ->
            task {
                let key = command.OrderId.ToString()
                let json = Encode.createOrder command |> Encode.toString 4

                let message = Message<string, string>(Key = key, Value = json)

                let! _ = producer.ProduceAsync("order-requests", message)

                ()
            }

    let compose (configuration: IConfiguration) : Environment =
        let connectionString = configuration.GetConnectionString "Default"
        let dataSource = NpgsqlDataSource.Create connectionString
        let connection = dataSource.CreateConnection()

        let producer = createProducer configuration
        let consumer = createConsumer configuration

        let StartSaga = createStartSaga producer

        { Consumer = consumer
          StartSaga = StartSaga
          GetOrder = get connection
          CreateOrder = create connection
          UpdateOrder = update connection }

let webApp (environment: Environment) =
    choose
        [ GET >=> choose [ routef "/order/%O" (getOrderHandler environment.GetOrder) ]

          POST
          >=> choose
                  [ route "/order"
                    >=> createOrderHandler environment.CreateOrder environment.StartSaga ]

          setStatusCode 404 >=> text "Not Found" ]

[<EntryPoint>]
let main args =
    let builder = WebApplication.CreateBuilder args

    let environment = CompositionRoot.compose builder.Configuration

    builder.Services.AddHostedService(fun _ -> new Worker(environment)) |> ignore
    builder.Services.AddGiraffe() |> ignore

    let app = builder.Build()

    app.UseGiraffe(webApp environment)
    app.Run()

    0
