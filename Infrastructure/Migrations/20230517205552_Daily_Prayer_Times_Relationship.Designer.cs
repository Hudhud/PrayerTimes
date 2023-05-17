﻿// <auto-generated />
using System;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

#nullable disable

namespace Infrastructure.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20230517205552_Daily_Prayer_Times_Relationship")]
    partial class Daily_Prayer_Times_Relationship
    {
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "7.0.5")
                .HasAnnotation("Relational:MaxIdentifierLength", 64);

            modelBuilder.Entity("Domain.Models.CityPrayerTimes", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    b.Property<string>("City")
                        .IsRequired()
                        .HasColumnType("longtext");

                    b.HasKey("Id");

                    b.ToTable("CityPrayerTimes");
                });

            modelBuilder.Entity("Domain.Models.DailyPrayerTimes", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    b.Property<string>("AsrHanafiTime")
                        .IsRequired()
                        .HasColumnType("longtext");

                    b.Property<string>("AsrTime")
                        .IsRequired()
                        .HasColumnType("longtext");

                    b.Property<int>("CityPrayerTimesId")
                        .HasColumnType("int");

                    b.Property<DateTime>("Date")
                        .HasColumnType("datetime(6)");

                    b.Property<string>("DhuhrTime")
                        .IsRequired()
                        .HasColumnType("longtext");

                    b.Property<string>("FajrTime")
                        .IsRequired()
                        .HasColumnType("longtext");

                    b.Property<string>("IshaTime")
                        .IsRequired()
                        .HasColumnType("longtext");

                    b.Property<string>("MaghribTime")
                        .IsRequired()
                        .HasColumnType("longtext");

                    b.Property<string>("SunriseTime")
                        .IsRequired()
                        .HasColumnType("longtext");

                    b.HasKey("Id");

                    b.HasIndex("CityPrayerTimesId");

                    b.ToTable("DailyPrayerTimes");
                });

            modelBuilder.Entity("Domain.Models.DailyPrayerTimes", b =>
                {
                    b.HasOne("Domain.Models.CityPrayerTimes", "CityPrayerTimes")
                        .WithMany("DailyPrayerTimesList")
                        .HasForeignKey("CityPrayerTimesId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("CityPrayerTimes");
                });

            modelBuilder.Entity("Domain.Models.CityPrayerTimes", b =>
                {
                    b.Navigation("DailyPrayerTimesList");
                });
#pragma warning restore 612, 618
        }
    }
}