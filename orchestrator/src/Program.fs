namespace Orchestrator

open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open Confluent.Kafka
open Giraffe
open Npgsql
open Types
open Handlers

module Program =
    module CompositionRoot =
        let private creatProducer (server: string) =
            let config = ProducerConfig(BootstrapServers = server)

            ProducerBuilder<string, string>(config).Build()

        let private createConsumer (server: string) (topics: string seq) =
            let config =
                ConsumerConfig(BootstrapServers = server, GroupId = "orchestrator-consumer")

            let consumer = ConsumerBuilder<string, string>(config).Build()

            consumer.Subscribe topics

            consumer

        let compose (configuration: IConfiguration) : Environment =
            let server = configuration.["Kafka:BootstrapServer"]
            let topics = configuration.GetSection("Kafka:Topics").Get<string array>()

            let connectionString = configuration.GetConnectionString "Default"
            let dataSource = NpgsqlDataSource.Create connectionString
            let connection = dataSource.CreateConnection()

            let connectionString = configuration.GetConnectionString "Default"
            let dataSource = NpgsqlDataSource.Create connectionString
            let connection = dataSource.CreateConnection()

            let producer = creatProducer server
            let consumer = createConsumer server topics

            let loggerFactory =
                LoggerFactory.Create(fun builder -> builder.AddConsole() |> ignore)

            let logger = loggerFactory.CreateLogger<Worker>()

            let publish (topic: string) (key: string) (message: string) =
                task {
                    let! _ = producer.ProduceAsync(topic, Message<string, string>(Key = key, Value = message))

                    ()
                }

            let querySaga = Database.get connection
            let createSaga = Database.create connection
            let updateSaga = Database.update connection

            { Publish = publish
              Consumer = consumer
              QuerySaga = querySaga
              CreateSaga = createSaga
              UpdateSaga = updateSaga
              Logger = logger }

    let webApp (environment: Environment) =
        choose
            [ GET >=> choose [ routef "/saga/%O" (getSagaHandler environment.QuerySaga) ]
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
