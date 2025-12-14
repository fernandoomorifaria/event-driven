module Handlers

open System
open Microsoft.AspNetCore.Http
open Giraffe
open Thoth.Json.Net
open Types

let getSagaHandler (querySaga: QuerySaga) (id: Guid) : HttpHandler =
    fun (next: HttpFunc) (ctx: HttpContext) ->
        task {
            let! saga = querySaga id

            match saga with
            | Some saga ->
                let json = Encode.saga saga |> Encode.toString 4
                ctx.SetContentType "application/json"
                return! text json next ctx
            | None ->
                ctx.SetStatusCode 404
                return! next ctx
        }
