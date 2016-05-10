#light

namespace Configuration

open System.Security.Cryptography
open System.Globalization
open System.Diagnostics
open System.Text
open System.IO
open System

module File =
    let parseCulture = new CultureInfo("en")
    let asFloat (p : string) = Single.Parse(p, parseCulture) |> float
    let asInt (p : string) = Int32.Parse(p, parseCulture)
    let asBool (p : string) = [ "true" ; "yes" ; "y" ; "on" ] |> Seq.exists ((=) (p.ToLower()))
    let asArray (p : string) = p.Split([|'|'|], StringSplitOptions.RemoveEmptyEntries) |> Seq.map (fun s -> s.Trim()) |> Seq.toList
    
    exception MissingConfigurationFileInformationException of string
    
    let mutable settings : Map<string, string> = Map.empty
    
    let getOr key defaultValue =
        match settings.TryFind key with
        | Some v -> v
        | None -> defaultValue
    let get key =
        match settings.TryFind key with
        | Some v -> v
        | None -> raise (MissingConfigurationFileInformationException key)
    
    let rec replaceThisOne (e : string) = function
        | [] -> e
        | (a,b)::m ->
            let v = e.Replace("$(" + a + ")", b)
            replaceThisOne v m
    let rec cmpMaps am bm =
        match (am, bm) with
        | ([], []) -> true
        | ([], _)
        | (_, []) -> false
        | (a::am, b::bm) when a = b -> cmpMaps am bm
        | _ -> false
    let rec replaceStrs m =
        let rec _replaceStrs ml nb =
            let nm =
                ml
                |> Seq.map (fun (a,b) -> (a, replaceThisOne b ml))
                |> Seq.toList
            if nb = 0 then
                nm
            else
                if cmpMaps ml nm then nm else _replaceStrs nm (nb - 1)
        _replaceStrs m 5
    let readSettings filePath =
        if File.Exists filePath |> not then
            Map.empty
        else
            let rec replaceAll kvl v =
                match kvl, v with
                | [], _ -> v
                | (a,b)::n, (va, vb:string) -> replaceAll n (va, vb.Replace("%(" + a + ")", b))
            File.ReadAllLines filePath
            |> Seq.map (fun l -> l.Trim())
            |> Seq.filter (fun l -> l.Length > 0)
            |> Seq.filter (fun l -> not(l.StartsWith "#"))
            |> Seq.filter (fun l -> l.Contains "=")
            |> Seq.map (fun l -> if not(l.Contains "#") then l else l.Substring(0, l.IndexOf "#"))
            |> Seq.map (fun l -> (l.Substring(0, l.IndexOf("=")), l.Substring(l.IndexOf("=") + 1)))
            |> Seq.map (fun (a,b) -> (a.Trim(), b.Trim()))
            |> Seq.map (replaceAll
                [
                    ("USER", Environment.UserName)
                    ("HOME", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile))
                ])
            |> Seq.toList
            |> replaceStrs
            |> Map.ofList
