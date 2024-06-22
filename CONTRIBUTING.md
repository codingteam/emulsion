Contributor Guide
=================

Prerequisites
-------------
To develop Emulsion, make sure you've installed the following tools:
- [.NET SDK][dotnet] 8.0 or later,
- [Node.js][node.js] 18:
  - if you use [nvm][] or [nvm-windows][], then run `nvm use 18`.

Build
-----
Build the project using the following shell command:

```console
$ dotnet build
```

Run
---
Run the application from sources using the following shell command:

```console
$ dotnet run --project ./Emulsion [optional-path-to-json-config-file]
```

Test
----
Execute the tests using the following shell command:

```console
$ dotnet test
```

Docker Publish
--------------
To build and push the container to Docker Hub, use the following shell commands:

```console
$ docker build -t codingteam/emulsion:$EMULSION_VERSION \
    -t codingteam/emulsion:latest .

$ docker login # if necessary
$ docker push codingteam/emulsion:$EMULSION_VERSION
$ docker push codingteam/emulsion:latest
```

where `$EMULSION_VERSION` is the version of the image to publish.

Updating the Database Structure
-------------------------------
If you want to update a database structure, you'll need to create a migration.

This article explains how to create a database migration using [EFCore.FSharp][efcore.fsharp].

1. Change the entity type (see `Emulsion.Database/Entities.fs`), update the `EmulsionDbContext` if required.
2. Run the following shell commands:

   ```console
   $ dotnet tool restore
   $ cd Emulsion.Database
   $ dotnet ef migrations add <migration-name>
   ```

[dotnet]: https://dot.net/
[efcore.fsharp]: https://github.com/efcore/EFCore.FSharp
[node.js]: https://nodejs.org/
[nvm]: https://github.com/nvm-sh/nvm
[nvm-windows]
