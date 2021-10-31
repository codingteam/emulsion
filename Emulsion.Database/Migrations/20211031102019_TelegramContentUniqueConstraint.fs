﻿// <auto-generated />
namespace Emulsion.Database.Migrations

open System
open Emulsion.Database
open Microsoft.EntityFrameworkCore
open Microsoft.EntityFrameworkCore.Infrastructure
open Microsoft.EntityFrameworkCore.Migrations

[<DbContext(typeof<EmulsionDbContext>)>]
[<Migration("20211031102019_TelegramContentUniqueConstraint")>]
type TelegramContentUniqueConstraint() =
    inherit Migration()

    override this.Up(migrationBuilder:MigrationBuilder) =
        migrationBuilder.Sql @"
            create unique index TelegramContents_Unique
            on TelegramContents(ChatUserName, MessageId, FileId)
        " |> ignore

    override this.Down(migrationBuilder:MigrationBuilder) =
        migrationBuilder.Sql @"
            drop index TelegramContents_Unique
        " |> ignore

    override this.BuildTargetModel(modelBuilder: ModelBuilder) =
        modelBuilder
            .HasAnnotation("ProductVersion", "5.0.10")
            |> ignore

        modelBuilder.Entity("Emulsion.Database.Entities.TelegramContent", (fun b ->

            b.Property<Int64>("Id")
                .IsRequired(true)
                .ValueGeneratedOnAdd()
                .HasColumnType("INTEGER") |> ignore
            b.Property<string>("ChatUserName")
                .IsRequired(false)
                .HasColumnType("TEXT") |> ignore
            b.Property<string>("FileId")
                .IsRequired(false)
                .HasColumnType("TEXT") |> ignore
            b.Property<Int64>("MessageId")
                .IsRequired(true)
                .HasColumnType("INTEGER") |> ignore

            b.HasKey("Id") |> ignore

            b.ToTable("TelegramContents") |> ignore

        )) |> ignore

