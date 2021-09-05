﻿namespace DynamicObj

open DynamicObj
open System.Reflection
open System.Runtime.CompilerServices

[<InternalsVisibleToAttribute("UnitTests")>]
do()

/// Represents an DynamicObj's counterpart
/// with immutability enabled only.
type ImmutableDynamicObj internal (map : Map<string, obj>) = 
    
    let mutable properties = map
    
    member private this.Properties 
        with get () =
            properties
        and set value =
            properties <- value
    
    // Copies the fields of one object to the other one
    // If their base is not ImmutableDynamicObj, then it
    // will go over fields from the base instance
    static member private copyMembers (ty : System.Type) sourceObject destinationObject =
        for fi in ty.GetFields(BindingFlags.Instance ||| BindingFlags.NonPublic ||| BindingFlags.Public) do
            let fieldValue = fi.GetValue sourceObject
            fi.SetValue(destinationObject, fieldValue)

    static member private newIfNeeded (object : 'a when 'a :> ImmutableDynamicObj) map : 'a =
        if obj.ReferenceEquals(map, object.Properties) then
            object
        else
            // otherwise we create a new instance
            let res = new 'a()

            // and then copy all current fields the new instance 
            ImmutableDynamicObj.copyMembers (typeof<'a>) object res
            res.Properties <- map
            res

    /// Empty instance
    new () = ImmutableDynamicObj Map.empty

    static member empty = ImmutableDynamicObj ()

    /// Indexes ; if no key found, throws
    member this.Item
        with get(index) =
            this.Properties.[index]

    /// Returns an instance with:
    /// 1. this property added if it wasn't present
    /// 2. this property updated otherwise
    static member add name newValue (object : #ImmutableDynamicObj) =
        object.Properties
        |> Map.add name newValue
        |> ImmutableDynamicObj.newIfNeeded object

    /// Returns an instance:
    /// 1. the same if there was no requested property
    /// 2. without the requested property if there was
    static member remove name (object : #ImmutableDynamicObj) =
        object.Properties
        |> Map.remove name
        |> ImmutableDynamicObj.newIfNeeded object


    /// Acts as add if the value is Some,
    /// returns the same object otherwise
    static member addOpt name newValue object =
        match newValue with
        | Some(value) -> object |> ImmutableDynamicObj.add name value
        | None -> object


    /// Acts as addOpt but maps the valid value 
    /// through the last argument
    static member addOptBy name newValue f object =
        match newValue with
        | Some(value) -> object |> ImmutableDynamicObj.add name (f value)
        | None -> object


    member this.TryGetValue name = 
        match this.Properties.TryGetValue name with
        | true, value ->  Some value
        | _ -> ReflectionUtils.tryGetPropertyValue this name


    member this.TryGetTypedValue<'a> name = 
        match this.TryGetValue name with
        | None -> None
        | Some o -> 
            match o with
            | :? 'a as o -> o |> Some
            | _ -> None

    static member format (object : #ImmutableDynamicObj) =

        let members = object.Properties |> Map.toList

        let rec loop (identationLevel:int) (membersLeft:(string*obj) list) (acc:string list) =
            let ident = [for i in 0 .. identationLevel-1 do yield "    "] |> String.concat ""
            match membersLeft with
            | [] -> acc |> List.rev |> String.concat System.Environment.NewLine
            | (key,item)::rest ->
                match item with
                | :? ImmutableDynamicObj as item -> 
                    let innerMembers = item.Properties |> Map.toList
                    let innerPrint = (loop (identationLevel + 1) innerMembers [])
                    loop identationLevel rest ($"{ident}?{key}:{System.Environment.NewLine}{innerPrint}" :: acc)
                | _ -> 
                    loop identationLevel rest ($"{ident}?{key}: {item}"::acc)
    
        loop 0 members []

    static member print (object : #ImmutableDynamicObj) = printfn "%s" (object |> ImmutableDynamicObj.format)

    override this.Equals o =
        match o with
        | :? ImmutableDynamicObj as other -> other.Properties = this.Properties
        | _ -> false

    override this.GetHashCode () = ~~~map.GetHashCode()

[<Extension>]
type ImmutableDynamicObjExtensions =

    /// Returns an instance with:
    /// 1. this property added if it wasn't present
    /// 2. this property updated otherwise
    /// use this one only from C#
    [<Extension>]
    static member AddItem (this, name, newValue) =
        ImmutableDynamicObj.add name newValue this

    /// Returns an instance:
    /// 1. the same if there was no requested property
    /// 2. without the requested property if there was
    /// use this one only from C#
    [<Extension>]
    static member RemoveItem (this, name) =
        ImmutableDynamicObj.remove name this    
        
    [<Extension>]
    static member Format (this) =
        ImmutableDynamicObj.format this        

    [<Extension>]
    static member Print (this) =
        ImmutableDynamicObj.print this