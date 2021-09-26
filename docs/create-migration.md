How to Create a Database Migration
==================================

This article explains how to create a database migration using [EFCore.FSharp][efcore.fsharp].

1. Change the entity type (see `Emulsion.Database/Models.fs`), update the `EmulsionDbContext` if required.
2. Run the following shell commands:

   ```console
   $ dotnet tool restore
   $ cd Emulsion.Database
   $ dotnet ef migrations add <migration-name>
   ```

[efcore.fsharp]: https://github.com/efcore/EFCore.FSharp
