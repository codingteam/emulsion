﻿// <auto-generated />
namespace Emulsion.Database.Migrations

open System
open Emulsion.Database
open Microsoft.EntityFrameworkCore
open Microsoft.EntityFrameworkCore.Infrastructure
open Microsoft.EntityFrameworkCore.Metadata
open Microsoft.EntityFrameworkCore.Migrations
open Microsoft.EntityFrameworkCore.Storage.ValueConversion

[<DbContext(typeof<EmulsionDbContext>)>]
type EmulsionDbContextModelSnapshot() =
    inherit ModelSnapshot()

    override this.BuildModel(modelBuilder: ModelBuilder) =
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

