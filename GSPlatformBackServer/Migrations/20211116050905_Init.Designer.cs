﻿// <auto-generated />
using System;
using GSPlatformBackServer.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

#nullable disable

namespace GSPlatformBackServer.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20211116050905_Init")]
    partial class Init
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder.HasAnnotation("ProductVersion", "6.0.0");

            modelBuilder.Entity("GSPlatformBackServer.Data.Invitation", b =>
                {
                    b.Property<int>("InvitationId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<string>("Code")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<DateTime>("Created")
                        .HasColumnType("TEXT");

                    b.Property<string>("Description")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<bool>("Invalidated")
                        .HasColumnType("INTEGER");

                    b.Property<DateTime>("TimeLimit")
                        .HasColumnType("TEXT");

                    b.Property<int>("UseCount")
                        .HasColumnType("INTEGER");

                    b.Property<int>("UseCountLimit")
                        .HasColumnType("INTEGER");

                    b.HasKey("InvitationId");

                    b.ToTable("Invitations");
                });

            modelBuilder.Entity("GSPlatformBackServer.Data.LogRecord", b =>
                {
                    b.Property<long>("LogRecordId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<string>("Content")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<DateTime>("Created")
                        .HasColumnType("TEXT");

                    b.Property<string>("Request")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<string>("RequestClient")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<string>("RequestServer")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<string>("UserToken")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.HasKey("LogRecordId");

                    b.ToTable("LogRecords");
                });

            modelBuilder.Entity("GSPlatformBackServer.Data.User", b =>
                {
                    b.Property<int>("UserId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<string>("Invitation")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<string>("RegisterIP")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<DateTime>("RegisterTime")
                        .HasColumnType("TEXT");

                    b.Property<string>("Token")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<string>("UserName")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.HasKey("UserId");

                    b.ToTable("Users");
                });

            modelBuilder.Entity("GSPlatformBackServer.Data.UserGroup", b =>
                {
                    b.Property<int>("UserGroupId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<string>("GroupName")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<int>("UserId")
                        .HasColumnType("INTEGER");

                    b.HasKey("UserGroupId");

                    b.HasIndex("UserId");

                    b.ToTable("UserGroups");
                });

            modelBuilder.Entity("GSPlatformBackServer.Data.UserGroup", b =>
                {
                    b.HasOne("GSPlatformBackServer.Data.User", "User")
                        .WithMany()
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("User");
                });
#pragma warning restore 612, 618
        }
    }
}