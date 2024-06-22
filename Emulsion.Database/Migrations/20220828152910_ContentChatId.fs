// SPDX-FileCopyrightText: 2024 Emulsion contributors <https://github.com/codingteam/emulsion>
//
// SPDX-License-Identifier: MIT

// <auto-generated />
namespace Emulsion.Database.Migrations

open System
open Emulsion.Database
open Microsoft.EntityFrameworkCore
open Microsoft.EntityFrameworkCore.Infrastructure
open Microsoft.EntityFrameworkCore.Migrations

[<DbContext(typeof<EmulsionDbContext>)>]
[<Migration("20220828152910_ContentChatId")>]
type ContentChatId() =
    inherit Migration()

    override this.Up(migrationBuilder:MigrationBuilder) =
        migrationBuilder.AddColumn<Int64>(
            name = "ChatId"
            ,table = "TelegramContents"
            ,``type`` = "INTEGER"
            ,nullable = false
            ,defaultValue = 0L
            ) |> ignore

        migrationBuilder.Sql @"
            drop index TelegramContents_Unique;

            create unique index TelegramContents_Unique
            on TelegramContents(ChatId, ChatUserName, MessageId, FileId, FileName, MimeType)
        " |> ignore

    override this.Down(migrationBuilder:MigrationBuilder) =
        migrationBuilder.Sql @"
            drop index TelegramContents_Unique;

            create unique index TelegramContents_Unique
            on TelegramContents(ChatUserName, MessageId, FileId, FileName, MimeType)
        " |> ignore

        migrationBuilder.DropColumn(
            name = "ChatId"
            ,table = "TelegramContents"
            ) |> ignore

    override this.BuildTargetModel(modelBuilder: ModelBuilder) =
        modelBuilder
            .HasAnnotation("ProductVersion", "5.0.10")
            |> ignore

        modelBuilder.Entity("Emulsion.Database.Entities.TelegramContent", (fun b ->

            b.Property<Int64>("Id")
                .IsRequired(true)
                .ValueGeneratedOnAdd()
                .HasColumnType("INTEGER") |> ignore
            b.Property<Int64>("ChatId")
                .IsRequired(true)
                .HasColumnType("INTEGER") |> ignore
            b.Property<string>("ChatUserName")
                .IsRequired(false)
                .HasColumnType("TEXT") |> ignore
            b.Property<string>("FileId")
                .IsRequired(false)
                .HasColumnType("TEXT") |> ignore
            b.Property<string>("FileName")
                .IsRequired(false)
                .HasColumnType("TEXT") |> ignore
            b.Property<Int64>("MessageId")
                .IsRequired(true)
                .HasColumnType("INTEGER") |> ignore
            b.Property<string>("MimeType")
                .IsRequired(false)
                .HasColumnType("TEXT") |> ignore

            b.HasKey("Id") |> ignore

            b.ToTable("TelegramContents") |> ignore

        )) |> ignore

