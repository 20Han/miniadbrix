﻿// <auto-generated />
using System;
using Events.API.DB;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Events.API.Migrations
{
    [DbContext(typeof(EventDBContext))]
    [Migration("20220712065430_initial")]
    partial class initial
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "6.0.6")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

            modelBuilder.Entity("Events.API.Entities.Event", b =>
                {
                    b.Property<Guid>("EventId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<DateTimeOffset>("CreateDate")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("EventName")
                        .HasColumnType("text");

                    b.Property<Guid?>("ParametersOrderId")
                        .HasColumnType("uuid");

                    b.Property<Guid>("UserId")
                        .HasColumnType("uuid");

                    b.HasKey("EventId");

                    b.HasIndex("ParametersOrderId");

                    b.ToTable("Events");
                });

            modelBuilder.Entity("Events.API.Entities.Parameters", b =>
                {
                    b.Property<Guid>("OrderId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<string>("Currency")
                        .HasColumnType("text");

                    b.Property<int>("Price")
                        .HasColumnType("integer");

                    b.HasKey("OrderId");

                    b.ToTable("Parameters");
                });

            modelBuilder.Entity("Events.API.Entities.Event", b =>
                {
                    b.HasOne("Events.API.Entities.Parameters", "Parameters")
                        .WithMany()
                        .HasForeignKey("ParametersOrderId");

                    b.Navigation("Parameters");
                });
#pragma warning restore 612, 618
        }
    }
}
