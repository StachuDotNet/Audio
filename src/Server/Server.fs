open System.IO
open Microsoft.Extensions.DependencyInjection
open Giraffe
open Saturn
open FSharp.Control.Tasks.ContextInsensitive
open Shared
open Thoth.Json.Net

let publicPath = Path.GetFullPath "../Client/public"
let audioPath = Path.GetFullPath "../../audio"
let port = 8085us

let tags = {
    Tags = [|
        { Token = "celeb"; Action = TagAction.PlayMusik (sprintf @"http://localhost:%d/api/audio/mp3/%s" port "Celebrate") }
        { Token = "stop"; Action = TagAction.StopMusik }
    |]
}

let allTags =
    tags.Tags
    |> Array.map (fun t -> t.Token,t)
    |> dict

let allTagsEndpoint =
    pipeline {
        set_header "Content-Type" "application/json"
        plug (fun next ctx -> task {
            let txt = TagList.Encoder tags |> Encode.toString 0
            return! setBodyFromString txt next ctx
        })
    }

let mp3Endpoint fileName =
    pipeline {
        set_header "Content-Type" "audio/mpeg"
        plug (fun next ctx -> task {
            let file = Path.Combine(audioPath,sprintf "mp3/%s.mp3" fileName)
            return! ctx.WriteFileStreamAsync true file None None
        })
    }

let tagEndpoint token =
    pipeline {
        set_header "Content-Type" "application/json"
        plug (fun next ctx -> task {
            let tag =
                match allTags.TryGetValue token with
                | true, t -> t
                | _ -> { Token = token; Action = TagAction.UnknownTag }

            let txt = Tag.Encoder tag |> Encode.toString 0
            return! setBodyFromString txt next ctx
        })
    }

let webApp =
    router {
        getf "/api/audio/mp3/%s" mp3Endpoint
        getf "/api/tags/%s" tagEndpoint
        get "/api/alltags" allTagsEndpoint
    }

let configureSerialization (services:IServiceCollection) =
    services

let app = application {
    url ("http://0.0.0.0:" + port.ToString() + "/")
    use_router webApp
    memory_cache
    use_static publicPath
    service_config configureSerialization
    use_gzip
}

run app
