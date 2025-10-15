using Microsoft.EntityFrameworkCore;

namespace WebShopV3.Models
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        public DbSet<Computer> Computers { get; set; }
        public DbSet<Component> Components { get; set; }
        public DbSet<ComputerComponent> ComputerComponents { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<ComputerOrder> ComputerOrders { get; set; }
        public DbSet<OrderType> OrderTypes { get; set; }
        public DbSet<Status> Statuses { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<UserType> UserTypes { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Конфигурация для ComputerComponent
            modelBuilder.Entity<ComputerComponent>()
                .HasKey(cc => new { cc.ComputerId, cc.ComponentId });

            modelBuilder.Entity<ComputerComponent>()
                .HasOne(cc => cc.Computer)
                .WithMany(c => c.ComputerComponents)
                .HasForeignKey(cc => cc.ComputerId);

            modelBuilder.Entity<ComputerComponent>()
                .HasOne(cc => cc.Component)
                .WithMany(c => c.ComputerComponents)
                .HasForeignKey(cc => cc.ComponentId);

            // Конфигурация для ComputerOrder
            modelBuilder.Entity<ComputerOrder>()
                .HasKey(co => new { co.ComputerId, co.OrderId });

            modelBuilder.Entity<ComputerOrder>()
                .HasOne(co => co.Computer)
                .WithMany(c => c.ComputerOrders)
                .HasForeignKey(co => co.ComputerId);

            modelBuilder.Entity<ComputerOrder>()
                .HasOne(co => co.Order)
                .WithMany(o => o.ComputerOrders)
                .HasForeignKey(co => co.OrderId);

            // Заполнение начальными данными
            modelBuilder.Entity<UserType>().HasData(
                new UserType { Id = 1, Name = "Админ" },
                new UserType { Id = 2, Name = "Пользователь" },
                new UserType { Id = 3, Name = "Менеджер" }
            );

            modelBuilder.Entity<OrderType>().HasData(
                new OrderType { Id = 1, Name = "Продажа" },
                new OrderType { Id = 2, Name = "Привоз" }
            );

            modelBuilder.Entity<Status>().HasData(
                new Status { Id = 1, Name = "Выполнен" },
                new Status { Id = 2, Name = "В ожидании" },
                new Status { Id = 3, Name = "Отменен" }
            );

            modelBuilder.Entity<User>().HasData(
                new User
                {
                    Id = 1,
                    Username = "admin",
                    Email = "admin@example.com",
                    PasswordHash = "admin123", // Пароль в открытом виде
                    FirstName = "Admin",
                    LastName = "ddd",
                    Phone = "123455",
                    UserTypeId = 1
                },
                new User
                {
                    Id = 2,
                    Username = "manager",
                    Email = "manager@example.com",
                    PasswordHash = "manager123",
                    FirstName = "Manager",
                    LastName = "ddd",
                    Phone = "123455",
                    UserTypeId = 3
                },
                new User
                {
                    Id = 3,
                    Username = "user",
                    Email = "user@example.com",
                    PasswordHash = "user123",
                    FirstName = "Regular",
                    LastName = "ddd",
                    Phone = "123455",
                    UserTypeId = 2
                }
            );
        }
    }
}
