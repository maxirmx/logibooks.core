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
// ``AS IS'' AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED
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
using Logibooks.Core.RestModels;
using Microsoft.AspNetCore.Mvc;
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
        public DbSet<OrderStatus> Statuses => Set<OrderStatus>();
        public DbSet<OrderCheckStatus> CheckStatuses => Set<OrderCheckStatus>();
        public DbSet<Order> Orders => Set<Order>();
        public DbSet<Country> Countries => Set<Country>();
        public DbSet<Company> Companies => Set<Company>();
        public async Task<bool> CheckAdmin(int cuid)
        {
            var user = await Users
                .AsNoTracking()
                .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                .Where(x => x.Id == cuid)
                .FirstOrDefaultAsync(); 
            return user != null && user.IsAdministrator();
        }
        public async Task<bool> CheckLogist(int cuid)
        {
            var user = await Users
                .AsNoTracking()
                .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                .Where(x => x.Id == cuid)
                .FirstOrDefaultAsync();
            return user != null && user.IsLogist();
        }
        public async Task<ActionResult<bool>> CheckAdminOrSameUser(int id, int cuid)
        {
            if (cuid == 0) return false;
            if (cuid == id) return true;
            return await CheckAdmin(cuid);
        }
        public bool CheckSameUser(int id, int cuid)
        {
            if (cuid == 0) return false;
            if (cuid == id) return true;
            return false;
        }
        public bool Exists(int id)
        {
            return Users.Any(e => e.Id == id);
        }
        public bool Exists(string email)
        {
            return Users.Any(u => u.Email.ToLower() == email.ToLower());
        }
        public async Task<UserViewItem?> UserViewItem(int id)
        {
            var user = await Users
                .AsNoTracking()
                .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                .Where(x => x.Id == id)
                .Select(x => new UserViewItem(x))
                .FirstOrDefaultAsync();
            return user ?? null;
        }
        public async Task<List<UserViewItem>> UserViewItems()
        {
            return await Users
                .AsNoTracking()
                .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                .Select(x => new UserViewItem(x))
                .ToListAsync();
        }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<UserRole>()
                .HasKey(ur => new { ur.UserId, ur.RoleId });

            modelBuilder.Entity<Country>()
                .HasKey(cc => cc.IsoNumeric);

            modelBuilder.Entity<Company>()
                .HasKey(cо => cо.Id);

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

            modelBuilder.Entity<Register>()
                .HasOne(o => o.Company)
                .WithMany(r => r.Registers)
                .HasForeignKey(o => o.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Order>()
                .HasOne(o => o.Register)
                .WithMany(r => r.Orders)
                .HasForeignKey(o => o.RegisterId);

            modelBuilder.Entity<Order>()
                .HasOne(o => o.Status)
                .WithMany(s => s.Orders)
                .HasForeignKey(o => o.StatusId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Order>()
                .HasOne(o => o.CheckStatus)
                .WithMany(s => s.Orders)
                .HasForeignKey(o => o.CheckStatusId)
                .OnDelete(DeleteBehavior.Restrict);
          
            modelBuilder.Entity<Role>().HasData(
                new Role { Id = 1, Name = "logist", Title = "Логист" },
                new Role { Id = 2, Name = "administrator", Title = "Администратор" }
            );

            modelBuilder.Entity<OrderStatus>().HasData(
                new OrderStatus { Id = 1, Title = "Не известен" }
            );

            modelBuilder.Entity<OrderCheckStatus>().HasData(
                new OrderCheckStatus { Id = 1, Title = "Загружен" },
                new OrderCheckStatus { Id = 101, Title = "Проблема" },
                new OrderCheckStatus { Id = 201, Title = "Проверен" }
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
        }
    }
}
