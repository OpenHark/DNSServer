#light

namespace DnsServer

open System.Text.RegularExpressions
open System.Runtime.Caching
open Microsoft.FSharp.Core
open System.ComponentModel
open System.Diagnostics
open System.Net.Sockets
open System.Threading
open System.IO.Pipes
open System.Text
open System.Net
open System.IO
open System
open Std

(**************************************
 * Settings
 ********
 * Defines the default values
 **************************************)
module StartSettings =
    open Configuration.File
    do settings <- readSettings "config.ini"
    
    let nbRetryOnError = getOr "NbRetryOnError" "3" |> asInt
    let mutable cacheTimeout = getOr "CacheTimeout" "10.0" |> asFloat
    
    let mutable port = getOr "LocalPort" "53" |> asInt
    let mutable remotePort = getOr "RemotePort" "53" |> asInt
    let mutable remoteIp = getOr "RemoteIp" "91.239.100.100"
    let mutable useCache = getOr "UseCache" "false" |> asBool
    let mutable isVerbose = getOr "IsVerbose" "true" |> asBool
    let mutable isErrorVerbose = getOr "IsErrorVerbose" "true" |> asBool
    let mutable blockedList = getOr "BlockedList" "" |> asArray
    let mutable format = getOr "Format" " >>> {DATE} > {IP}:{PORT} > {DNS}"
    let mutable blockedFormat = getOr "BlockedFormat" " >X> {DATE} > {IP}:{PORT} > {DNS} [BLOCKED]"
    let mutable scope =
        let scope = function
        | "any" -> IPAddress.Any
        | "local"
        | _ -> IPAddress.Loopback
        getOr "Scope" "local" |> scope

(**************************************
 * Writters
 **************************************)
module Writter =
    let _writeLine isVerbose (wl : string -> unit) = lazy(
        match isVerbose with
        | true -> wl
        | false -> ignore
    )

    let wlMutex = new Object()

    let writeLine str =
        lock wlMutex (fun () ->
            (_writeLine StartSettings.isVerbose Console.WriteLine).Value str
        )
    let writeErrorLine str =
        lock wlMutex (fun () ->
            (_writeLine StartSettings.isErrorVerbose Console.Error.WriteLine).Value str
        )

(**************************************
 * DNS String formatting
 **************************************)
module Formatter =
    let rec dnsToString data = seq {
        let b : byte = Array.get data 0
        match int(b) with
        | 0 -> ()
        | size ->
            yield System.Text.Encoding.UTF8.GetString(data.[1..size])
            yield! data.[size + 1..] |> dnsToString
    }

    let replaceFormat (ep : IPEndPoint) s format =
        format
        |> String.replace "{IP}" (ep.Address.ToString())
        |> String.replace "{PORT}" (ep.Port.ToString())
        |> String.replace "{DNS}" s
        |> String.replace "{DATE}" (DateTime.Now.ToString())

(**************************************
 * Cache management
 **************************************)
module Cache =
    let cacheMutex = new Object()
    let cacheDns = MemoryCache.Default
    let getFromCache dns =
        match cacheDns.Get(dns) with
        | :? (byte array) as v -> Some(v)
        | _ -> None
    let cachePolicy =
        let policy = new CacheItemPolicy()
        policy.AbsoluteExpiration <- DateTimeOffset.Now.AddSeconds(StartSettings.cacheTimeout)
        policy
    let setToCache dns data =
        cacheDns.Add(dns, data, cachePolicy) |> ignore

(**************************************
 * Accept/Reject
 **************************************)
module Rejection =
    let reject data =
        [ 2; 3; 6; 7 ] |> Seq.iter (fun i -> Array.set data i <| byte(0))
        data

    let accept dns data =
        let isRejected data = [ 2; 3; 6; 7 ] |> Seq.map (Array.get data) |> Seq.forall ((=) (byte(0)))
        let rec getFromRemote nb =
            if nb <= 0 then
                raise (new System.Net.Sockets.SocketException())
            let timeout = TimeSpan.FromSeconds(1.0)
            use acceptUdpDest = new UdpClient()
            acceptUdpDest.Send(data, Array.length data, StartSettings.remoteIp, StartSettings.remotePort) |> ignore
            let asyncResult = acceptUdpDest.BeginReceive( null, null )
            if asyncResult.AsyncWaitHandle.WaitOne(timeout) && asyncResult.IsCompleted then
                try
                    let remoteEP = null
                    acceptUdpDest.EndReceive( asyncResult, ref remoteEP )
                with
                | _ -> getFromRemote (nb - 1)
            else
                getFromRemote (nb - 1)
        if StartSettings.useCache then
            lock Cache.cacheMutex (fun() ->
                match Cache.getFromCache dns with
                | Some(d) -> d
                | _ ->
                    let d = getFromRemote StartSettings.nbRetryOnError
                    if isRejected d |> not then
                        Cache.setToCache dns d
                    d
            )
        else
            getFromRemote StartSettings.nbRetryOnError

module Runtime =
    (**************************************
    * Client management
    **************************************)
    let loop (data : byte array) rep = async {
        let str =
            data.[12..]
            |> Formatter.dnsToString
            |> Seq.reduce (fun a b -> a + "." + b)
            
        let matcher s = Regex.Match(str, s).Success
        let acc =
            StartSettings.blockedList
            |> Seq.map matcher
            |> Seq.forall not
            
        let compute data = if acc then Rejection.accept str data else Rejection.reject data
                
        let print ep =
            let printResult format = Formatter.replaceFormat ep str format |> Writter.writeLine
            if acc then
                StartSettings.format |> printResult
            else
                StartSettings.blockedFormat |> printResult
                
        try
            let finalData = compute data
            use udpRespClient = new UdpClient()
            udpRespClient.Send(finalData, finalData.Length, !rep) |> ignore
            print !rep
        with
        | :? System.Net.Sockets.SocketException as ex -> ()
        | ex -> Writter.writeErrorLine ex.Message
    }

    (**************************************
    * Start - Main loop
    ********
    * Waits for clients
    **************************************)
    let start () =
        let udpClient = new UdpClient(StartSettings.port)
        Writter.writeLine <| "Server started on port " + StartSettings.port.ToString()
        while true do
            let rep = ref(new IPEndPoint(IPAddress.Loopback, 0))
            let data = udpClient.Receive(rep)
            loop data rep
            |> Async.Start
            
    (**************************************
    * Stop
    ********
    * Kills all instances of DnsServer
    **************************************)
    let stop () =
        let currentProcess = Process.GetCurrentProcess()
        let currentProcessId = currentProcess.Id
        let currentProcessPath = currentProcess.MainModule.ModuleName
        for p in Process.GetProcesses() do
            try
                if p.MainModule.ModuleName = currentProcessPath && p.Id <> currentProcessId then
                    "Killing " + p.Id.ToString() |> Console.WriteLine
                    p.Kill()
                    p.WaitForExit()
            with
            | _ -> ()

(**************************************
 * Entry point - Argument management
 **************************************)
module EntryPoint =
    let rec checkStartArgs args =
        match args with
        | [] -> true
        | _ ->
            try
                match (args.Head, args.Tail) with
                | ("-port", Parse.UInt16(n)::e) ->
                    StartSettings.port <- int n
                    checkStartArgs e
                    
                | ("-ct", Parse.Float(n)::e)
                | ("-cachetimeout", Parse.Float(n)::e) ->
                    StartSettings.cacheTimeout <- float n
                    checkStartArgs e
                    
                | ("-remoteport", Parse.UInt16(n)::e) ->
                    StartSettings.remotePort <- int n
                    checkStartArgs e
                    
                | ("-remoteip", (Parse.Ip(_) & ip)::e) ->
                    StartSettings.remoteIp <- ip
                    checkStartArgs e
                    
                | ("-nv", e)
                | ("-notverbose", e) ->
                    StartSettings.isVerbose <- false
                    checkStartArgs e
                    
                | ("-nev", e)
                | ("-noterrorverbose", e) ->
                    StartSettings.isErrorVerbose <- false
                    checkStartArgs e
                    
                | ("-c", e)
                | ("-cache", e) ->
                    StartSettings.useCache <- true
                    checkStartArgs e
                    
                | ("-f", format::e)
                | ("-format", format::e) ->
                    StartSettings.format <- format
                    checkStartArgs e
                    
                | ("-bf", format::e)
                | ("-blockedformat", format::e) ->
                    StartSettings.blockedFormat <- format
                    checkStartArgs e
                    
                | ("-scope", "any"::e) ->
                    StartSettings.scope <- IPAddress.Any
                    checkStartArgs e
                | ("-scope", "local"::e) ->
                    StartSettings.scope <- IPAddress.Loopback
                    checkStartArgs e
                    
                | ("-bl", path::e)
                | ("-blockedlist", path::e) ->
                    let newBlockedList =
                        File.ReadAllLines path
                        |> Seq.map String.trim
                        |> Seq.nfilter String.isEmpty
                        |> Seq.nfilter (String.startsWith "#")
                        |> Seq.map String.trim
                        |> Seq.toList
                    StartSettings.blockedList <- List.append StartSettings.blockedList newBlockedList
                    checkStartArgs e
                    
                | ("-bls", value::e)
                | ("-blockedliststr", value::e) ->
                    StartSettings.blockedList <- List.append StartSettings.blockedList [ String.trim value ]
                    checkStartArgs e
                    
                | ("-help", _)
                | ("-h", _)
                | ("?", _) ->
                    // Display help
                    Std.displayHeader ()
                    let sep   = ("===============================::=========================")
                    let space = ("                                :")
                    console {
                        return "Usages"
                        yield! [
                            " " + information.APP_NAME + " start <arguments>"
                            " " + information.APP_NAME + " restart <arguments>"
                            ""
                            "Arguments (case sensitive) :"
                            sep
                            " -help / -h / ?                :: Display this help"
                            sep
                            " -port <port>                  :: Define the listening port"
                            "                                : Default = " + StartSettings.port.ToString()
                            space
                            " -scope <scope>                :: Define the network scope"
                            "                                : /!\\ The position of this argument"
                            "                                :      is important"
                            "                                : <scope> = | local"
                            "                                :           | any"
                            "                                : Default = " + (if StartSettings.scope = IPAddress.Any then "any" else "local")
                            space
                            " -remoteport <port>            :: Define the remote port to send the"
                            "                                : DNS requests to"
                            "                                : Default = " + StartSettings.remotePort.ToString()
                            space
                            " -remoteip <ip>                :: Define the remote IP to send the"
                            "                                : DNS requests to"
                            "                                : Default = " + StartSettings.remoteIp
                            sep
                            " -bf / -blockedformat <format> :: Define the format of the erroneous"
                            "                                : output values"
                            "                                : Default = " + StartSettings.blockedFormat
                            space
                            " -f / -format <format>         :: Define the format of the output values"
                            "                                : Default = " + StartSettings.format
                            sep
                            " -nv / -notverbose             :: Do not display messages"
                            " -nev / -noterrorverbose       :: Do not display errors"
                            sep
                            " -c / -cache                   :: Use an internal cache"
                            " -ct / -cachetimeout <timeout> :: Define the cache timeout of an entry"
                            " -bl / -blockedlist <filepath> :: Add a file containing blocked domains"
                            " -bls / -blockedliststr <dmn>  :: Add a blocked domain"
                        ]
                        return ""
                    }
                    false
                    
                | _ ->
                    "Argument \"" + args.Head + "\" not understood." |> Console.Error.WriteLine
                    false
            with
            | ex -> "Argument \"" + args.Head + "\" throwed an exception : " + ex.Message + "." |> Console.Error.WriteLine
                    false
                    
                    
    let rec main args =
        if Array.length args = 0 then
            main [| "help" |]
        else
            let pkg = args.[0].ToLower()
            let argsl = args |> Array.toList |> List.skip 1
            
            match (pkg, Array.length args - 1) with
            | ("start", _) ->
                if checkStartArgs argsl then
                    Runtime.start ()
                    
            | ("restart", _) ->
                if checkStartArgs argsl then
                    Runtime.stop ()
                    Runtime.start ()
                    
            | ("stop", 0) -> Runtime.stop()
                    
            | _ ->
                // Display help
                Std.displayHeader ()
                console {
                    return "Usages"
                    yield " " + information.APP_NAME + " start [args]"
                    yield " " + information.APP_NAME + " start ?"
                    yield " " + information.APP_NAME + " restart [args]"
                    yield " " + information.APP_NAME + " restart ?"
                    yield " " + information.APP_NAME + " stop"
                    return ""
                }
            0
