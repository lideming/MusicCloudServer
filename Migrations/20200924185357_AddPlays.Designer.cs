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
    [Migration("20200924185357_AddPlays")]
    partial class AddPlays
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "3.1.7");

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
                        .IsConcurrencyToken()
                        .HasColumnType("INTEGER");

                    b.Property<int>("visibility")
                        .HasColumnType("INTEGER");

                    b.HasKey("id");

                    b.ToTable("lists");
                });

            modelBuilder.Entity("MCloudServer.LoginRecord", b =>
                {
                    b.Property<string>("token")
                        .HasColumnType("TEXT");

                    b.Property<int>("UserId")
                        .HasColumnType("INTEGER");

                    b.Property<DateTime>("last_used")
                        .HasColumnType("TEXT");

                    b.Property<DateTime>("login_date")
                        .HasColumnType("TEXT");

                    b.HasKey("token");

                    b.HasIndex("UserId");

                    b.ToTable("logins");
                });

            modelBuilder.Entity("MCloudServer.PlayRecord", b =>
                {
                    b.Property<int>("id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<int>("trackid")
                        .HasColumnType("INTEGER");

                    b.Property<int>("uid")
                        .HasColumnType("INTEGER");

                    b.HasKey("id");

                    b.HasIndex("trackid");

                    b.HasIndex("uid");

                    b.ToTable("plays");
                });

            modelBuilder.Entity("MCloudServer.Track", b =>
                {
                    b.Property<int>("id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<string>("artist")
                        .HasColumnType("TEXT");

                    b.Property<string>("files")
                        .HasColumnType("TEXT");

                    b.Property<int>("length")
                        .HasColumnType("INTEGER");

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

                    b.Property<int>("version")
                        .IsConcurrencyToken()
                        .HasColumnType("INTEGER");

                    b.Property<int>("visibility")
                        .HasColumnType("INTEGER");

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

            modelBuilder.Entity("MCloudServer.LoginRecord", b =>
                {
                    b.HasOne("MCloudServer.User", "User")
                        .WithMany()
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("MCloudServer.PlayRecord", b =>
                {
                    b.HasOne("MCloudServer.Track", "Track")
                        .WithMany()
                        .HasForeignKey("trackid")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("MCloudServer.User", "User")
                        .WithMany()
                        .HasForeignKey("uid")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });
#pragma warning restore 612, 618
        }
    }
}
