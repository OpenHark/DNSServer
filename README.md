# DNS Server

## Plan

- [Introduction](#introduction)
- [Project structure](#project-structure)
- [Compilation](#compilation)
- [Execution](#execution)
- [Installation](#installation)
  - [Windows [XP | Vista | 7 | 8 | 10]](#installation-windows)
- [Tasks](#tasks)
- [License](#license)

## <a name="information"></a> Introduction

This is a DNS Server.

Its aim was first to block the DNS requests used by Windows 10
to send "telemetries". Now, it can be used to block all kind
of recipient like Facebook, Google, etc. Because they use
different names for their services, it is easier for such a
software to block them.

## <a name="project-structure"></a> Project structure

```
dnsserver
|:: .vscode        - Visual Studio Code folder (optional)
|:: out            - Output folder
|:: src            - Source code
    |:: lib        - Standard code source
```

> Because this is a light project, I made the choice to
focus on F#. If the project get bigger, it may be a good
thing to reconsider this choice. The weakness of F# is its
poor project structuration ability. A clear object oriented
language is more suited for maintainability of big projects.

> Of course, I first made this software in different languages
(C++, Java, C#), but the F# version is far smaller (less
code for the same result) and fast enough to provide a very
nice result.

## <a name="compilation"></a> Compilation

You need to have installed fsc.exe (F# compiler). If you don't
have the file available with PATH, you will have have to add
it or to change the **makefile** to specify its full path.

If you are using Visual Studio Code, you just have to press
Ctrl+Shift+B. It will run the command 'make'.

If you are not using Visual Studio Code, open a terminal on
the root folder (where there is the file **makefile**) and run
the following command : ` make ` or with full specification :
` make build `.

There is a *deploy* option in the **makefile**. It will copy
the executable to the final folder.

## <a name="execution"></a> Execution

| Command | Description |
| --- | --- |
| `dnsserver start [args]` | Start the DNS server |
| `dnsserver start ?` | Display the help about the start option |
| `dnsserver restart [args]` | Stop all instances of the server and restart it with the specified arguments |
| `dnsserver restart ?` | Display the help about the restart option (same arguments as start) |
| `dnsserver stop` | Stop all instances of the DNS server |

When the help is displayed, the default arguments are displayed too.
To be sure an argument will not change from a version to another, you
should specify yours, even if the default argument value is the one you
want.

Here are some arguments taken by `dnsserver start` and `dnsserver restart` :

| Command | Description |
| --- | --- |
| `-help / -h / ?` | Display the help |
| `-port <port>` | Define the  |
| `-scope {any / local}` | Define the network scope (the position of this argument is important) |
| `-remoteport <port>` | Define the remote port to send the DNS requests to |
| `-remoteip <ip>` | Define the remote IP to send the DNS requests to |
| `-f / -format <format>` | Define the format of the output values |
| `-bf / -blockedformat <format>` | Define the format of the erroneous output values |
| `-nv / -notverbose` | Do not display messages |
| `-nev / -noterrorverbose` | Do not display errors |
| `-c / -cache` | Use an internal cache |
| `-ct / -cachetimeout <timeout>` | Define the cache timeout of an entry |
| `-bl / -blockedlist <filepath>` | Add a file containing blocked domains |
| `-bls / -blockedliststr <domain>` | Add a blocked domain |

## <a name="installation"></a> Installation

### <a name="installation-windows"></a> Windows [XP | Vista | 7 | 8 | 10]

To change the DNS settings of your computer under Windows, you can
follow this great [tutorial](http://mintywhite.com/windows-7/change-dns-server-windows-7/).
In the "Use the following DNS server addresses" section at step 5,
you will have to specify your DNS server IP.

- If you started it in local (on your computer), you will have to use `127.0.0.1`.
- If you started it on a machine in your network, use its local IP.
- If you started it on a machine in another network, so be sure its router is well configured.

If your purpose is to block some services, do not specify an alternate DNS server
IP in your settings, otherwise it may bypass yours.

## <a name="tasks"></a> Tasks

- [ ] Add an *include* command in dns filter file
  - [ ] Include local files
  - [ ] Include remote files
- [ ] Add the ability to use DNS mapping (like host file in Windows)

## <a name="license"></a> License

![GNU AGPLv3](https://www.gnu.org/graphics/agplv3-155x51.png)

[[Can-Cannot-Must description]](https://www.tldrlegal.com/l/agpl3)
[[Learn more]](http://www.gnu.org/licenses/agpl-3.0.html)
