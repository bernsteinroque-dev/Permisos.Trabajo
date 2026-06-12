using Microsoft.EntityFrameworkCore;
using PTS_Synthon.Models;

namespace PTS_Synthon.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Permiso> Permisos => Set<Permiso>();
    public DbSet<Proveedor> Proveedores => Set<Proveedor>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasSequence<int>("seq_NumeroPermiso", "dbo")
            .StartsAt(7610)
            .IncrementsBy(1);

        modelBuilder.HasSequence<int>("seq_NumeroProveedor", "dbo")
            .StartsAt(1)
            .IncrementsBy(1);

        modelBuilder.Entity<Permiso>(entity =>
        {
            entity.ToTable("Permisos");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.NumeroPermiso)
                .HasDefaultValueSql("NEXT VALUE FOR dbo.seq_NumeroPermiso");
            entity.Property(e => e.Tipo).IsRequired().HasMaxLength(20);
            entity.Property(e => e.Estado).HasMaxLength(20).HasDefaultValue("pending");
            entity.Property(e => e.FechaCreacion).HasDefaultValueSql("GETUTCDATE()");
            entity.Property(e => e.CreadoPor).HasMaxLength(100).IsRequired();
            entity.Property(e => e.EmpresaPlanta).HasMaxLength(100);
            entity.Property(e => e.VerificacionesJson).HasColumnType("nvarchar(max)");
            entity.Property(e => e.PeligrosJson).HasColumnType("nvarchar(max)");
            entity.Property(e => e.EppGeneralJson).HasColumnType("nvarchar(max)");
            entity.Property(e => e.CheckListJson).HasColumnType("nvarchar(max)");
            entity.Property(e => e.Descripcion).HasColumnType("nvarchar(max)");
            entity.Property(e => e.Observaciones).HasColumnType("nvarchar(max)");
            entity.Ignore(e => e.UsuarioCreadorDisplay);

            entity.HasOne(e => e.Proveedor)
                .WithMany(p => p.Permisos)
                .HasForeignKey(e => e.ProveedorId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(e => e.NumeroPermiso).HasDatabaseName("IX_Permisos_NumeroPermiso");
            entity.HasIndex(e => e.Estado).HasDatabaseName("IX_Permisos_Estado");
            entity.HasIndex(e => e.CreadoPor).HasDatabaseName("IX_Permisos_CreadoPor");
        });

        modelBuilder.Entity<Proveedor>(entity =>
        {
            entity.ToTable("Proveedores");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.NumeroProveedor)
                .HasDefaultValueSql("NEXT VALUE FOR dbo.seq_NumeroProveedor");
            entity.Property(e => e.RazonSocial).IsRequired().HasMaxLength(200);
            entity.Property(e => e.CreadoPor).HasMaxLength(100).IsRequired();
            entity.Property(e => e.ObsDocumentacion).HasColumnType("nvarchar(max)");
            entity.Property(e => e.Observaciones).HasColumnType("nvarchar(max)");
        });
    }
}
