// Copyright (C) 2025 Maxim [maxirmx] Samsonov (www.sw.consulting)
// All rights reserved.
// This file is a part of Logibooks Core application
//
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions
// are met:
// 1. Redistributions of source code must retain the above copyright
// notice, this list of conditions and the following disclaimer.
// 2. Redistributions in binary form must reproduce the above copyright
// notice, this list of conditions and the following disclaimer in the
// documentation and/or other materials provided with the distribution.
//
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
// 'AS IS' AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED
// TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR
// PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDERS OR CONTRIBUTORS
// BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR
// CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF
// SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS
// INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN
// CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
// ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
// POSSIBILITY OF SUCH DAMAGE.

using Logibooks.Core.Models;
using Microsoft.EntityFrameworkCore;
namespace Logibooks.Core.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options) { }
        public DbSet<User> Users => Set<User>();
        public DbSet<Role> Roles => Set<Role>();
        public DbSet<UserRole> UserRoles => Set<UserRole>();
        public DbSet<Register> Registers => Set<Register>();
        public DbSet<ParcelStatus> Statuses => Set<ParcelStatus>();
        public DbSet<ParcelCheckStatus> CheckStatuses => Set<ParcelCheckStatus>();
        public DbSet<BaseParcel> Orders => Set<BaseParcel>();
        public DbSet<WbrOrder> WbrOrders => Set<WbrOrder>();
        public DbSet<OzonOrder> OzonOrders => Set<OzonOrder>();
        public DbSet<Country> Countries => Set<Country>();
        public DbSet<Company> Companies => Set<Company>();
        public DbSet<WordMatchType> WordMatchTypes => Set<WordMatchType>();
        public DbSet<StopWord> StopWords => Set<StopWord>();
        public DbSet<FeacnOrder> FeacnOrders => Set<FeacnOrder>();
        public DbSet<FeacnPrefix> FeacnPrefixes => Set<FeacnPrefix>();
        public DbSet<FeacnPrefixException> FeacnPrefixExceptions => Set<FeacnPrefixException>();
        public DbSet<CustomsProcedure> CustomsProcedures => Set<CustomsProcedure>();
        public DbSet<BaseOrderFeacnPrefix> BaseOrderFeacnPrefixes => Set<BaseOrderFeacnPrefix>();
        public DbSet<TransportationType> TransportationTypes => Set<TransportationType>();
        public DbSet<ParcelView> ParcelViews => Set<ParcelView>();
        public DbSet<KeyWord> KeyWords => Set<KeyWord>();
        public DbSet<KeyWordFeacnCode> KeyWordFeacnCodes => Set<KeyWordFeacnCode>();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<UserRole>()
                .HasKey(ur => new { ur.UserId, ur.RoleId });

            modelBuilder.Entity<Country>()
                .HasKey(cc => cc.IsoNumeric);

            modelBuilder.Entity<Company>()
                .HasKey(co => co.Id);

            modelBuilder.Entity<Company>()
                .HasOne(c => c.Country)
                .WithMany(cn => cn.Companies)
                .HasForeignKey(c => c.CountryIsoNumeric)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Company>()
                .HasIndex(c => c.Inn)
                .IsUnique();

            modelBuilder.Entity<UserRole>()
                .HasOne(ur => ur.User)
                .WithMany(u => u.UserRoles)
                .HasForeignKey(ur => ur.UserId);

            modelBuilder.Entity<UserRole>()
                .HasOne(ur => ur.Role)
                .WithMany(r => r.UserRoles)
                .HasForeignKey(ur => ur.RoleId);

            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique();

            modelBuilder.Entity<ParcelView>()
                .HasOne(pv => pv.User)
                .WithMany()
                .HasForeignKey(pv => pv.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ParcelView>()
                .HasOne(pv => pv.BaseOrder)
                .WithMany()
                .HasForeignKey(pv => pv.BaseOrderId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Register>()
                .HasOne(o => o.Company)
                .WithMany(r => r.Registers)
                .HasForeignKey(o => o.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Register>()
                .HasOne(o => o.TheOtherCompany)
                .WithMany(r => r.TheOtherRegisters)
                .HasForeignKey(o => o.TheOtherCompanyId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Register>()
                .HasOne(r => r.TheOtherCountry)
                .WithMany()
                .HasForeignKey(r => r.TheOtherCountryCode)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Register>()
                .HasOne(r => r.TransportationType)
                .WithMany()
                .HasForeignKey(r => r.TransportationTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Register>()
                .HasOne(r => r.CustomsProcedure)
                .WithMany()
                .HasForeignKey(r => r.CustomsProcedureId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<BaseParcel>()
                .HasOne(o => o.Register)
                .WithMany(r => r.Orders)
                .HasForeignKey(o => o.RegisterId);

            modelBuilder.Entity<BaseParcel>()
                .HasOne(o => o.Status)
                .WithMany(s => s.Orders)
                .HasForeignKey(o => o.StatusId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<BaseParcel>()
                .HasOne(o => o.CheckStatus)
                .WithMany(s => s.Orders)
                .HasForeignKey(o => o.CheckStatusId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<BaseParcel>()
                .HasOne(o => o.Country)
                .WithMany()
                .HasForeignKey(o => o.CountryCode)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<BaseParcel>().ToTable("base_orders");
            modelBuilder.Entity<WbrOrder>().ToTable("wbr_orders");
            modelBuilder.Entity<OzonOrder>().ToTable("ozon_orders");

            modelBuilder.Entity<WbrOrder>()
                .HasBaseType<BaseParcel>();

            modelBuilder.Entity<OzonOrder>()
                .HasBaseType<BaseParcel>();

            modelBuilder.Entity<OzonOrder>()
                .HasIndex(o => o.PostingNumber);

            modelBuilder.Entity<BaseOrderStopWord>()
                .HasKey(bosw => new { bosw.BaseOrderId, bosw.StopWordId });

            modelBuilder.Entity<BaseOrderStopWord>()
                .HasOne(bosw => bosw.BaseOrder)
                .WithMany(bo => bo.BaseOrderStopWords)
                .HasForeignKey(bosw => bosw.BaseOrderId);

            modelBuilder.Entity<BaseOrderStopWord>()
                .HasOne(bosw => bosw.StopWord)
                .WithMany(sw => sw.BaseOrderStopWords)
                .HasForeignKey(bosw => bosw.StopWordId);

            modelBuilder.Entity<BaseOrderFeacnPrefix>()
                .HasKey(bofp => new { bofp.BaseOrderId, bofp.FeacnPrefixId });

            modelBuilder.Entity<BaseOrderFeacnPrefix>()
                .HasOne(bofp => bofp.BaseOrder)
                .WithMany(bo => bo.BaseOrderFeacnPrefixes)
                .HasForeignKey(bofp => bofp.BaseOrderId);

            modelBuilder.Entity<BaseOrderFeacnPrefix>()
                .HasOne(bofp => bofp.FeacnPrefix)
                .WithMany(fp => fp.BaseOrderFeacnPrefixes)
                .HasForeignKey(bofp => bofp.FeacnPrefixId);

            modelBuilder.Entity<FeacnPrefix>()
                .HasOne(fp => fp.FeacnOrder)
                .WithMany(fo => fo.FeacnPrefixes)
                .HasForeignKey(fp => fp.FeacnOrderId);

            modelBuilder.Entity<FeacnPrefixException>()
                .HasOne(e => e.FeacnPrefix)
                .WithMany(p => p.FeacnPrefixExceptions)
                .HasForeignKey(e => e.FeacnPrefixId);

            modelBuilder.Entity<Role>().HasData(
                new Role { Id = 1, Name = "logist", Title = "Логист" },
                new Role { Id = 2, Name = "administrator", Title = "Администратор" }
            );

            modelBuilder.Entity<ParcelStatus>().HasData(
                new ParcelStatus { Id = 1, Title = "Не известен" }
            );

            modelBuilder.Entity<ParcelCheckStatus>().HasData(
                new ParcelCheckStatus { Id = (int)ParcelCheckStatusCode.NotChecked, Title = "Не проверен" },
                new ParcelCheckStatus { Id = (int)ParcelCheckStatusCode.HasIssues, Title = "Запрет" },
                new ParcelCheckStatus { Id = (int)ParcelCheckStatusCode.InvalidFeacnFormat, Title = "Неправильный формат ТН ВЭД" },
                new ParcelCheckStatus { Id = (int)ParcelCheckStatusCode.NonexistingFeacn, Title = "Несуществующий ТН ВЭД" },
                new ParcelCheckStatus { Id = (int)ParcelCheckStatusCode.MarkedByPartner, Title = "Отмечено партнёром" },
                new ParcelCheckStatus { Id = (int)ParcelCheckStatusCode.NoIssues, Title = "Ок" },
                new ParcelCheckStatus { Id = (int)ParcelCheckStatusCode.Approved, Title = "Согласовано" }
            );

            modelBuilder.Entity<User>().HasData(
                new User
                {
                    Id = 1,
                    FirstName = "Maxim",
                    LastName = "Samsonov",
                    Patronymic = "",
                    Email = "maxirmx@sw.consulting",
                    Password = "$2b$12$eOXzlwFzyGVERe0sNwFeJO5XnvwsjloUpL4o2AIQ8254RT88MnsDi"
                }
            );

            modelBuilder.Entity<User>().HasData(
                new User
                {
                    Id = 2,
                    FirstName = "Эльдар",
                    LastName = "Сергутов",
                    Patronymic = "Юрьевич",
                    Email = "director@global-tc.ru",
                    Password = "$2a$11$KUvUbYg79OvDjq9xFKw1Ge4AYboMse4xduI.ZD54vp28zkb4DjWfK"
                }
            );

            modelBuilder.Entity<User>().HasData(
                new User
                {
                    Id = 3,
                    FirstName = "Полина",
                    LastName = "Баландина",
                    Patronymic = "Анатольевна",
                    Email = "wild@global-tc.ru",
                    Password = "$2a$11$zA1ohkl1U6UGbkhUlNvtTexHkbQ7CtiFnHTSsBc4xz8a5BY8D9yDS"
                }
            );

            modelBuilder.Entity<Country>().HasData(
                new Country
                {
                    IsoNumeric = 643,
                    IsoAlpha2 = "RU",
                    NameEnShort = "Russian Federation (the)",
                    NameEnFormal = "the Russian Federation",
                    NameEnOfficial = "Russian Federation",
                    NameEnCldr = "Rusia",
                    NameRuShort = "Российская Федерация",
                    NameRuFormal = "Российская Федерация",
                    NameRuOfficial = "Российская Федерация",
                    LoadedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                }
            );

            modelBuilder.Entity<Company>().HasData(
                new Company
                {
                    Id = 1,
                    Inn = "7704217370",
                    Kpp = "997750001",
                    Name = "ООО \"Интернет Решения\"",
                    ShortName = "",
                    CountryIsoNumeric = 643,
                    PostalCode = "123112",
                    City = "Москва",
                    Street = "Пресненская набережная д.10, пом.1, этаж 41, ком.6"
                },
                new Company
                {
                    Id = 2,
                    Inn = "9714053621",
                    Kpp = "507401001",
                    Name = "",
                    ShortName = "ООО \"РВБ\"",
                    CountryIsoNumeric = 643,
                    PostalCode = "",
                    City = "д. Коледино",
                    Street = "Индустриальный Парк Коледино, д.6, стр.1"
                }
            );

            modelBuilder.Entity<TransportationType>().HasData(
                new TransportationType { Id = 1, Code = TransportationTypeCode.Avia, Name = "Авиа" },
                new TransportationType { Id = 2, Code = TransportationTypeCode.Auto, Name = "Авто" }
            );

            modelBuilder.Entity<UserRole>().HasData(
                new UserRole { UserId = 1, RoleId = 1 }
            );
            modelBuilder.Entity<UserRole>().HasData(
                new UserRole { UserId = 1, RoleId = 2 }
            );
            modelBuilder.Entity<UserRole>().HasData(
                new UserRole { UserId = 2, RoleId = 1 }
            );
            modelBuilder.Entity<UserRole>().HasData(
                new UserRole { UserId = 2, RoleId = 2 }
            );
            modelBuilder.Entity<UserRole>().HasData(
                new UserRole { UserId = 3, RoleId = 1 }
            );

            modelBuilder.Entity<FeacnOrder>().HasData(
                new FeacnOrder
                {
                    Id = 1,
                    Title = "Решение Комиссии Таможенного союза от 18 июня 2010 г. N 317 \"О применении ветеринарно-санитарных мер в Евразийском экономическом союзе\"",
                    Url = "10sr0317",
                    Comment = "Подлежит ветеринарному контролю"
                },
                new FeacnOrder
                {
                    Id = 2,
                    Title = "Решение Комиссии Таможенного союза от 18 июня 2010 г. N 318 \"Об обеспечении карантина растений в Евразийском экономическом союзе\"",
                    Url = "10sr0318",
                    Comment = "Подлежит карантинному фитосанитарному контролю"
                },
                new FeacnOrder
                {
                    Id = 3,
                    Title = "Приказ ФТС России от 12 мая 2011 г. N 971 \"О компетенции таможенных органов по совершению таможенных операций в отношении драгоценных металлов и драгоценных камней\"",
                    Url = "11pr0971",
                    Comment = "Операции в отношении драгоценных металлов и драгоценных камней"
                },
                new FeacnOrder
                {
                    Id = 4,
                    Title = "Постановление Правительства РФ от 09.03.2022 № 311 \"О мерах по реализации Указа Президента Российской Федерации от 8 марта 2022 г. N 100\"",
                    Url = "22ps0311",
                    Comment = "Временный запрет на вывоз"
                },
                new FeacnOrder
                {
                    Id = 5,
                    Title = "Постановление Правительства Российской Федерации от 9 марта 2022 г. N 312 \"О введении на временной основе разрешительного порядка вывоза отдельных видов товаров за пределы территории Российской Федерации\"",
                    Url = "22ps0312",
                    Comment = "Разрешительный порядок вывоза"
                }
            );

            modelBuilder.Entity<CustomsProcedure>().HasData(
                new CustomsProcedure { Id = 1, Code = 10, Name = "Экспорт" },
                new CustomsProcedure { Id = 2, Code = 60, Name = "Реимпорт" }
            );

            modelBuilder.Entity<WordMatchType>().HasData(
                new WordMatchType { Id = (int)WordMatchTypeCode.ExactSymbols, Name = "Точная последовательность букв, цифр и пробелов" },
                new WordMatchType { Id = (int)WordMatchTypeCode.ExactWord, Name = "Точное слово" },
                new WordMatchType { Id = (int)WordMatchTypeCode.Phrase, Name = "Фраза (последовательность слов)" },
                new WordMatchType { Id = (int)WordMatchTypeCode.WeakMorphology, Name = "Слово и его формы (Золото -> c золотом, о золоте, ...)" },
                new WordMatchType { Id = (int)WordMatchTypeCode.StrongMorphology, Name = "Слово и однокоренные (Золото -> золотой, золотистый, ...)" }
            );

            modelBuilder.Entity<KeyWord>()
                .HasOne(k => k.MatchType)
                .WithMany(mt => mt.KeyWords)
                .HasForeignKey(k => k.MatchTypeId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<KeyWordFeacnCode>()
                .HasKey(kwfc => new { kwfc.KeyWordId, kwfc.FeacnCode });

            modelBuilder.Entity<KeyWordFeacnCode>()
                .HasOne(kwfc => kwfc.KeyWord)
                .WithMany(kw => kw.KeyWordFeacnCodes)
                .HasForeignKey(kwfc => kwfc.KeyWordId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<BaseOrderKeyWord>()
                .HasKey(bokw => new { bokw.BaseOrderId, bokw.KeyWordId });

            modelBuilder.Entity<BaseOrderKeyWord>()
                .HasOne(bokw => bokw.BaseOrder)
                .WithMany(bo => bo.BaseOrderKeyWords)
                .HasForeignKey(bokw => bokw.BaseOrderId);

            modelBuilder.Entity<BaseOrderKeyWord>()
                .HasOne(bokw => bokw.KeyWord)
                .WithMany(kw => kw.BaseOrderKeyWords)
                .HasForeignKey(bokw => bokw.KeyWordId);
        }
    }
}
