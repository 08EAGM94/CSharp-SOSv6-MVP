using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace HexArch.Data.Models;

public partial class Sosv6DbContext : DbContext
{
    public Sosv6DbContext()
    {
    }

    public Sosv6DbContext(DbContextOptions<Sosv6DbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Bitacora> Bitacoras { get; set; }

    public virtual DbSet<Contacto> Contactos { get; set; }

    public virtual DbSet<Empresa> Empresas { get; set; }

    public virtual DbSet<Equipo> Equipos { get; set; }

    public virtual DbSet<Tipo> Tipos { get; set; }

    public virtual DbSet<Usuario> Usuarios { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Bitacora>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pk_bitacoras");

            entity.Property(e => e.ActividadesRealizadas)
                .IsUnicode(false)
                .HasColumnName("Actividades_realizadas");
            entity.Property(e => e.ContactoId).HasColumnName("Contacto_id");
            entity.Property(e => e.EquipoId).HasColumnName("Equipo_id");
            entity.Property(e => e.Estatus)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.FirmaCliente)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("Firma_cliente");
            entity.Property(e => e.Monto).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.Observaciones).IsUnicode(false);
            entity.Property(e => e.Servicio).IsUnicode(false);
            entity.Property(e => e.UsuarioId).HasColumnName("Usuario_id");
            entity.Property(e => e.Visibilidad)
                .HasMaxLength(10)
                .IsUnicode(false);

            entity.HasOne(d => d.Contacto).WithMany(p => p.Bitacoras)
                .HasForeignKey(d => d.ContactoId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_bitacoras_cotacto");

            entity.HasOne(d => d.Equipo).WithMany(p => p.Bitacoras)
                .HasForeignKey(d => d.EquipoId)
                .HasConstraintName("fk_bitacoras_equipo");

            entity.HasOne(d => d.Usuario).WithMany(p => p.Bitacoras)
                .HasForeignKey(d => d.UsuarioId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_bitacoras_usuario");
        });

        modelBuilder.Entity<Contacto>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pk_contactos");

            entity.Property(e => e.EmpresaId).HasColumnName("Empresa_id");
            entity.Property(e => e.NombreCompleto)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("Nombre_completo");
            entity.Property(e => e.Visibilidad)
                .HasMaxLength(10)
                .IsUnicode(false);

            entity.HasOne(d => d.Empresa).WithMany(p => p.Contactos)
                .HasForeignKey(d => d.EmpresaId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_contactos_empresa");
        });

        modelBuilder.Entity<Empresa>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pk_empresas");

            entity.HasIndex(e => e.NombreComercial, "uq_nombre_comercial").IsUnique();

            entity.Property(e => e.Atencion)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.CalleNumero)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("Calle_numero");
            entity.Property(e => e.Colonia)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.DirigirseCon)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("Dirigirse_con");
            entity.Property(e => e.Email)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.EntreCalles)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("Entre_calles");
            entity.Property(e => e.Horario)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Localidad)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.NombreComercial)
                .HasMaxLength(255)
                .IsUnicode(false)
                .UseCollation("Modern_Spanish_CI_AI")
                .HasColumnName("Nombre_comercial");
            entity.Property(e => e.RazonSocial)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("Razon_social");
            entity.Property(e => e.Telefonos)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Visibilidad)
                .HasMaxLength(10)
                .IsUnicode(false);
        });

        modelBuilder.Entity<Equipo>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pk_equipos");

            entity.HasIndex(e => e.NumeroSerie, "uq_numero_serie").IsUnique();

            entity.Property(e => e.EmpresaId).HasColumnName("Empresa_id");
            entity.Property(e => e.Marca)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Modelo)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.NumeroInventario).HasColumnName("Numero_inventario");
            entity.Property(e => e.NumeroSerie)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("Numero_serie");
            entity.Property(e => e.TipoId).HasColumnName("Tipo_id");
            entity.Property(e => e.Visibilidad)
                .HasMaxLength(10)
                .IsUnicode(false);

            entity.HasOne(d => d.Empresa).WithMany(p => p.Equipos)
                .HasForeignKey(d => d.EmpresaId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_equipos_empresa");

            entity.HasOne(d => d.Tipo).WithMany(p => p.Equipos)
                .HasForeignKey(d => d.TipoId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_equipos_tipo");
        });

        modelBuilder.Entity<Tipo>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pk_tipos");

            entity.HasIndex(e => e.Tipo1, "uq_tipo").IsUnique();

            entity.Property(e => e.Tipo1)
                .HasMaxLength(255)
                .IsUnicode(false)
                .UseCollation("Modern_Spanish_CI_AI")
                .HasColumnName("Tipo");
            entity.Property(e => e.Visibilidad)
                .HasMaxLength(10)
                .IsUnicode(false);
        });

        modelBuilder.Entity<Usuario>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("pk_usuarios");

            entity.HasIndex(e => e.Alias, "uq_alias").IsUnique();

            entity.Property(e => e.Alias)
                .HasMaxLength(100)
                .IsUnicode(false)
                .UseCollation("Modern_Spanish_CI_AI");
            entity.Property(e => e.Apellidos)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.Contrasena).IsUnicode(false);
            entity.Property(e => e.Firma)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Nombre)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.Privilegio)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.Visibilidad)
                .HasMaxLength(10)
                .IsUnicode(false);
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
