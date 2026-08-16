using System;
using System.Collections.Generic;
using System.Text;
using AveroNova.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AveroNova.Infrastructure.Persistence
{
    public class AppDbContext : DbContext
    {
        //Constructor
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {

        }
        //DbSet Properties
        public DbSet<User> Users { get; set; }
        public DbSet<Role> Roles { get; set; }
        public DbSet<UserRole> UserRoles { get; set; }
        public DbSet<Permission> Permissions { get; set; }
        public DbSet<RolePermission> RolePermissions { get; set; }
        public DbSet<Company> Companies { get; set; }
        public DbSet<Subscription> Subscriptions { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        }

    }
}
