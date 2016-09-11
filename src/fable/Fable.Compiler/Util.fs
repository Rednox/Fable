namespace Fable

[<AutoOpen>]
module Extensions =
    type System.Collections.Generic.Dictionary<'TKey,'TValue> with
        member dic.GetOrAdd(key, addFn) =
            match dic.TryGetValue(key) with
            | true, v -> v
            | false, _ ->
                let v = addFn()
                dic.Add(key, v)
                v  
        member dic.AddOrUpdate(key, addFn, updateFn) =
            let v =
                match dic.TryGetValue(key) with
                | true, v ->
                    dic.Remove(key) |> ignore
                    updateFn key v
                | false, _ -> addFn key
            dic.Add(key, v)
            v

module Map = 
    let findOrNew<'T when 'T : (new : unit -> 'T)> (k: string) (m: Map<string, obj>) =
        match Map.tryFind k m with
        | Some(:? 'T as x) -> x
        | _ -> new 'T()

    let findOrRun<'T> (f: unit->'T) (k: string) (m: Map<string, obj>) =
        match Map.tryFind k m with
        | Some(:? 'T as x) -> x
        | _ -> f()

module Option = 
    let toBool (f: 'T->bool) (opt: 'T option) =
        match opt with Some x when f x -> true | _ -> false 

module Json =
    open System.Reflection
    open FSharp.Reflection
    open Newtonsoft.Json
    
    let isErasedUnion (t: System.Type) =
        t.Name = "FSharpOption`1" ||
        FSharpType.IsUnion t &&
#if NETSTANDARD1_6
            t.GetTypeInfo().GetCustomAttributes(true)
#else        
            t.GetCustomAttributes true
#endif
            |> Seq.exists (fun a -> (a.GetType ()).Name = "EraseAttribute")
            
    let getErasedUnionValue (v: obj) =
        match FSharpValue.GetUnionFields (v, v.GetType()) with
        | _, [|v|] -> Some v
        | _ -> None
            
    type ErasedUnionConverter() =
        inherit JsonConverter()
        override x.CanConvert t = isErasedUnion t
        override x.ReadJson(reader, t, v, serializer) =
            failwith "Not implemented"
        override x.WriteJson(writer, v, serializer) =
            match getErasedUnionValue v with
            | Some v -> serializer.Serialize(writer, v) 
            | None -> writer.WriteNull()

    type LocationEraser() =
        inherit JsonConverter()
        override x.CanConvert t = typeof<AST.Babel.Node>.IsAssignableFrom(t)
        override x.ReadJson(reader, t, v, serializer) =
            failwith "Not implemented"
        override x.WriteJson(writer, v, serializer) =
            writer.WriteStartObject()
            v.GetType().GetProperties()
            |> Seq.filter (fun p -> p.Name <> "loc")
            |> Seq.iter (fun p ->
                writer.WritePropertyName(p.Name)
                serializer.Serialize(writer, p.GetValue(v)))
            writer.WriteEndObject()
