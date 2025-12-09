namespace Orchestrator

open System
open System.Collections.Generic
open System.Linq
open System.Threading.Tasks
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Confluent.Kafka

module Program =

    [<EntryPoint>]
    let main args =
        let builder = Host.CreateApplicationBuilder(args)

        let server = builder.Configuration.["Kafka:BootstrapServer"]

        (* TODO: Read from appsettings *)
        let topics = [| "orders" |]

        let producer (server: string) =
            let config = ProducerConfig(BootstrapServers = server)

            ProducerBuilder<Null, string>(config).Build()

        let consumer (server: string) (topics: string seq) =
            let config =
                ConsumerConfig(
                    BootstrapServers = server,
                    GroupId = "orchestrator-consumer",
                    AutoOffsetReset = AutoOffsetReset.Earliest,

                    EnableAutoCommit = true
                )

            let consumer = ConsumerBuilder<Null, string>(config).Build()

            consumer.Subscribe topics

            consumer

        (* TODO: Composition root *)
        builder.Services.AddSingleton(producer server) |> ignore

        builder.Services.AddSingleton(consumer server topics) |> ignore

        builder.Services.AddHostedService<Worker>() |> ignore

        builder.Build().Run()

        0
