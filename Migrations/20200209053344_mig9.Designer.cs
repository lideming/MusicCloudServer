﻿// <auto-generated />
using System;
using MCloudServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace MCloudServer.Migrations
{
    [DbContext(typeof(DbCtx))]
    [Migration("20200209053344_mig9")]
    partial class mig9
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "3.1.0");

            modelBuilder.Entity("MCloudServer.Comment", b =>
                {
                    b.Property<int>("id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<string>("content")
                        .HasColumnType("TEXT");

                    b.Property<DateTime>("date")
                        .HasColumnType("TEXT");

                    b.Property<string>("tag")
                        .HasColumnType("TEXT")
                        .HasMaxLength(20);

                    b.Property<int>("uid")
                        .HasColumnType("INTEGER");

                    b.HasKey("id");

                    b.HasIndex("tag");

                    b.ToTable("comments");
                });

            modelBuilder.Entity("MCloudServer.ConfigItem", b =>
                {
                    b.Property<string>("Key")
                        .HasColumnType("TEXT");

                    b.Property<string>("Value")
                        .HasColumnType("TEXT");

                    b.HasKey("Key");

                    b.ToTable("config");
                });

            modelBuilder.Entity("MCloudServer.List", b =>
                {
                    b.Property<int>("id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<string>("name")
                        .HasColumnType("TEXT");

                    b.Property<int>("owner")
                        .HasColumnType("INTEGER");

                    b.Property<string>("trackids")
                        .HasColumnType("TEXT");

                    b.Property<int>("version")
                        .HasColumnType("INTEGER");

                    b.HasKey("id");

                    b.ToTable("lists");
                });

            modelBuilder.Entity("MCloudServer.Track", b =>
                {
                    b.Property<int>("id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<string>("artist")
                        .HasColumnType("TEXT");

                    b.Property<string>("lyrics")
                        .HasColumnType("TEXT");

                    b.Property<string>("name")
                        .HasColumnType("TEXT");

                    b.Property<int>("owner")
                        .HasColumnType("INTEGER");

                    b.Property<int>("size")
                        .HasColumnType("INTEGER");

                    b.Property<string>("url")
                        .HasColumnType("TEXT");

                    b.HasKey("id");

                    b.ToTable("tracks");
                });

            modelBuilder.Entity("MCloudServer.User", b =>
                {
                    b.Property<int>("id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<string>("last_playing")
                        .HasColumnType("TEXT");

                    b.Property<string>("lists")
                        .HasColumnType("TEXT");

                    b.Property<string>("passwd")
                        .HasColumnType("TEXT");

                    b.Property<int>("role")
                        .HasColumnType("INTEGER");

                    b.Property<string>("username")
                        .HasColumnType("TEXT");

                    b.Property<int>("version")
                        .IsConcurrencyToken()
                        .HasColumnType("INTEGER");

                    b.HasKey("id");

                    b.HasIndex("username")
                        .IsUnique();

                    b.ToTable("users");
                });
#pragma warning restore 612, 618
        }
    }
}
