#light

namespace DnsServer

#load "src/lib/configuration.fsx"
#load "src/information.fsx"
#load "src/lib/std.fsx"
#load "src/dnsserver.fsx"

module Startup =
    [<EntryPoint>]
    let main (args : string[]) =
        DnsServer.EntryPoint.main(args)
