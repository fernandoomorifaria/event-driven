namespace Orchestrator

open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open Confluent.Kafka
open Npgsql
open Types

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

            { Publish = publish
              Consumer = consumer
              Connection = connection
              Logger = logger }



    [<EntryPoint>]
    let main args =
        let builder = Host.CreateApplicationBuilder args

        let environment = CompositionRoot.compose builder.Configuration

        builder.Services.AddHostedService(fun _ -> new Worker(environment)) |> ignore

        builder.Build().Run()

        0
