module Types

open System
open System.Text.Json.Serialization
open System.Threading.Tasks

type CreateOrderCommand =
    { [<JsonPropertyName("orderId")>]
      OrderId: Guid

      [<JsonPropertyName("customerId")>]
      CustomerId: int

      [<JsonPropertyName("productId")>]
      ProductId: int

      [<JsonPropertyName("amount")>]
      Amount: decimal }

type StartSaga = CreateOrderCommand -> Task<unit>

type Environment = { StartSaga: StartSaga }

type CreateOrderRequest =
    { CustomerId: int
      ProductId: int
      Amount: decimal }
