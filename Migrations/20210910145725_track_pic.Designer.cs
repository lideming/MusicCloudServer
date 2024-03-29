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
    [Migration("20210910145725_track_pic")]
    partial class track_pic
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "5.0.7");

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
                        .HasMaxLength(20)
                        .HasColumnType("TEXT");

                    b.Property<int>("uid")
                        .HasColumnType("INTEGER");

                    b.HasKey("id");

                    b.HasIndex("tag");

                    b.HasIndex("uid");

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

                    b.Property<string>("audioprofile")
                        .HasColumnType("TEXT");

                    b.Property<int>("listid")
                        .HasColumnType("INTEGER");

                    b.Property<DateTime>("time")
                        .HasColumnType("TEXT");

                    b.Property<int>("trackid")
                        .HasColumnType("INTEGER");

                    b.Property<int>("uid")
                        .HasColumnType("INTEGER");

                    b.HasKey("id");

                    b.HasIndex("audioprofile");

                    b.HasIndex("listid");

                    b.HasIndex("time");

                    b.HasIndex("trackid");

                    b.HasIndex("uid");

                    b.ToTable("plays");
                });

            modelBuilder.Entity("MCloudServer.StoredFile", b =>
                {
                    b.Property<int>("id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<string>("path")
                        .HasColumnType("TEXT");

                    b.Property<long>("size")
                        .HasColumnType("INTEGER");

                    b.HasKey("id");

                    b.ToTable("file");
                });

            modelBuilder.Entity("MCloudServer.Track", b =>
                {
                    b.Property<int>("id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<string>("album")
                        .HasColumnType("TEXT");

                    b.Property<string>("albumArtist")
                        .HasColumnType("TEXT");

                    b.Property<string>("artist")
                        .HasColumnType("TEXT");

                    b.Property<int?>("fileRecordId")
                        .HasColumnType("INTEGER");

                    b.Property<int>("groupId")
                        .HasColumnType("INTEGER");

                    b.Property<int>("length")
                        .HasColumnType("INTEGER");

                    b.Property<string>("lyrics")
                        .HasColumnType("TEXT");

                    b.Property<string>("name")
                        .HasColumnType("TEXT");

                    b.Property<int>("owner")
                        .HasColumnType("INTEGER");

                    b.Property<int?>("pictureFileId")
                        .HasColumnType("INTEGER");

                    b.Property<int>("version")
                        .IsConcurrencyToken()
                        .HasColumnType("INTEGER");

                    b.Property<int>("visibility")
                        .HasColumnType("INTEGER");

                    b.HasKey("id");

                    b.HasIndex("fileRecordId");

                    b.HasIndex("owner");

                    b.HasIndex("pictureFileId");

                    b.ToTable("tracks");
                });

            modelBuilder.Entity("MCloudServer.TrackFile", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<int>("Bitrate")
                        .HasColumnType("INTEGER");

                    b.Property<string>("ConvName")
                        .HasColumnType("TEXT");

                    b.Property<int>("FileID")
                        .HasColumnType("INTEGER");

                    b.Property<string>("Format")
                        .HasColumnType("TEXT");

                    b.Property<int>("TrackID")
                        .HasColumnType("INTEGER");

                    b.HasKey("Id");

                    b.HasIndex("FileID");

                    b.HasIndex("TrackID");

                    b.ToTable("trackFile");
                });

            modelBuilder.Entity("MCloudServer.TrackList", b =>
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

                    b.HasIndex("owner");

                    b.ToTable("lists");
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

            modelBuilder.Entity("MCloudServer.Comment", b =>
                {
                    b.HasOne("MCloudServer.User", "user")
                        .WithMany()
                        .HasForeignKey("uid")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("user");
                });

            modelBuilder.Entity("MCloudServer.LoginRecord", b =>
                {
                    b.HasOne("MCloudServer.User", "User")
                        .WithMany()
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("User");
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

                    b.Navigation("Track");

                    b.Navigation("User");
                });

            modelBuilder.Entity("MCloudServer.Track", b =>
                {
                    b.HasOne("MCloudServer.StoredFile", "fileRecord")
                        .WithMany()
                        .HasForeignKey("fileRecordId");

                    b.HasOne("MCloudServer.User", "user")
                        .WithMany()
                        .HasForeignKey("owner")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("MCloudServer.StoredFile", "pictureFile")
                        .WithMany()
                        .HasForeignKey("pictureFileId");

                    b.Navigation("fileRecord");

                    b.Navigation("pictureFile");

                    b.Navigation("user");
                });

            modelBuilder.Entity("MCloudServer.TrackFile", b =>
                {
                    b.HasOne("MCloudServer.StoredFile", "File")
                        .WithMany()
                        .HasForeignKey("FileID")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("MCloudServer.Track", "Track")
                        .WithMany("files")
                        .HasForeignKey("TrackID")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("File");

                    b.Navigation("Track");
                });

            modelBuilder.Entity("MCloudServer.TrackList", b =>
                {
                    b.HasOne("MCloudServer.User", "user")
                        .WithMany()
                        .HasForeignKey("owner")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("user");
                });

            modelBuilder.Entity("MCloudServer.Track", b =>
                {
                    b.Navigation("files");
                });
#pragma warning restore 612, 618
        }
    }
}
