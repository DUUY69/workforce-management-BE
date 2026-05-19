using Microsoft.EntityFrameworkCore;

namespace WorkforceManagement.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<Store> Stores => Set<Store>();
    public DbSet<EmployeeStore> EmployeeStores => Set<EmployeeStore>();
    public DbSet<Shift> Shifts => Set<Shift>();
    public DbSet<ShiftRegistration> ShiftRegistrations => Set<ShiftRegistration>();
    public DbSet<Attendance> Attendances => Set<Attendance>();
    public DbSet<SalaryCoefficient> SalaryCoefficients => Set<SalaryCoefficient>();
    public DbSet<Payroll> Payrolls => Set<Payroll>();
    public DbSet<PayrollDetail> PayrollDetails => Set<PayrollDetail>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.Entity<User>().ToTable("Users");
        mb.Entity<Employee>().ToTable("Employees");
        mb.Entity<Store>().ToTable("Stores");
        mb.Entity<EmployeeStore>().ToTable("EmployeeStores");
        mb.Entity<Shift>().ToTable("Shifts");
        mb.Entity<ShiftRegistration>().ToTable("ShiftRegistrations");
        mb.Entity<Attendance>().ToTable("Attendances");
        mb.Entity<SalaryCoefficient>().ToTable("SalaryCoefficients");
        mb.Entity<Payroll>().ToTable("Payrolls");
        mb.Entity<PayrollDetail>().ToTable("PayrollDetails");
        mb.Entity<Payment>().ToTable("Payments");
        mb.Entity<RefreshToken>().ToTable("RefreshTokens");

        // User
        mb.Entity<User>(e =>
        {
            e.HasIndex(x => x.Username).IsUnique();
            e.HasIndex(x => x.Email).IsUnique();
            e.Property(x => x.Role).HasMaxLength(20);
        });

        // Employee
        mb.Entity<Employee>(e =>
        {
            e.HasIndex(x => x.EmployeeCode).IsUnique();
            e.HasOne(x => x.User).WithOne(x => x.Employee)
                .HasForeignKey<Employee>(x => x.UserId);
        });

        // EmployeeStore — unique (EmployeeId, StoreId)
        mb.Entity<EmployeeStore>(e =>
        {
            e.HasIndex(x => new { x.EmployeeId, x.StoreId }).IsUnique();
            e.HasOne(x => x.Employee).WithMany(x => x.EmployeeStores).HasForeignKey(x => x.EmployeeId);
            e.HasOne(x => x.Store).WithMany(x => x.EmployeeStores).HasForeignKey(x => x.StoreId);
        });

        mb.Entity<ShiftRegistration>(e =>
        {
            e.HasIndex(x => new { x.EmployeeId, x.StoreId, x.WorkDate, x.StartTime, x.EndTime }).IsUnique();
            e.HasOne(x => x.Employee).WithMany(x => x.ShiftRegistrations).HasForeignKey(x => x.EmployeeId);
            e.HasOne(x => x.Shift).WithMany(x => x.ShiftRegistrations).HasForeignKey(x => x.ShiftId).IsRequired(false);
            e.HasOne(x => x.Store).WithMany().HasForeignKey(x => x.StoreId);
            e.HasOne(x => x.Reviewer).WithMany().HasForeignKey(x => x.ReviewedBy).IsRequired(false);
        });

        // Attendance — unique (EmployeeId, StoreId, WorkDate)
        mb.Entity<Attendance>(e =>
        {
            e.HasIndex(x => new { x.EmployeeId, x.StoreId, x.WorkDate }).IsUnique();
            e.Ignore(x => x.WorkedHours); // computed in app layer
            e.HasOne(x => x.Employee).WithMany(x => x.Attendances).HasForeignKey(x => x.EmployeeId);
            e.HasOne(x => x.Store).WithMany(x => x.Attendances).HasForeignKey(x => x.StoreId);
            e.HasOne(x => x.CreatedByUser).WithMany().HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.UpdatedByUser).WithMany().HasForeignKey(x => x.UpdatedBy).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
        });

        // SalaryCoefficient — no delete, append-only
        mb.Entity<SalaryCoefficient>(e =>
        {
            e.HasOne(x => x.Employee).WithMany(x => x.SalaryCoefficients).HasForeignKey(x => x.EmployeeId);
            e.HasOne(x => x.CreatedByUser).WithMany().HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.Restrict);
        });

        // Payroll — unique (StoreId, Month, Year)
        mb.Entity<Payroll>(e =>
        {
            e.HasIndex(x => new { x.StoreId, x.Month, x.Year }).IsUnique();
            e.HasOne(x => x.Store).WithMany(x => x.Payrolls).HasForeignKey(x => x.StoreId);
            e.HasOne(x => x.CreatedByUser).WithMany().HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.ApprovedByUser).WithMany().HasForeignKey(x => x.ApprovedBy).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
        });

        // PayrollDetail — unique (PayrollId, EmployeeId)
        mb.Entity<PayrollDetail>(e =>
        {
            e.HasIndex(x => new { x.PayrollId, x.EmployeeId }).IsUnique();
            e.Ignore(x => x.NetSalary); // computed in app layer
            e.HasOne(x => x.Payroll).WithMany(x => x.Details).HasForeignKey(x => x.PayrollId);
            e.HasOne(x => x.Employee).WithMany(x => x.PayrollDetails).HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        });

        // Payment
        mb.Entity<Payment>(e =>
        {
            e.HasOne(x => x.Payroll).WithMany(x => x.Payments).HasForeignKey(x => x.PayrollId);
            e.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.RecordedByUser).WithMany().HasForeignKey(x => x.RecordedBy).OnDelete(DeleteBehavior.Restrict);
        });

        // RefreshToken
        mb.Entity<RefreshToken>(e =>
        {
            e.HasIndex(x => x.Token).IsUnique();
            e.HasOne(x => x.User).WithMany(x => x.RefreshTokens).HasForeignKey(x => x.UserId);
        });
    }
}
