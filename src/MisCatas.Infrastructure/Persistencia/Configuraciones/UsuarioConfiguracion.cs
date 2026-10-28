using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MisCatas.Domain.Entidades;

namespace MisCatas.Infrastructure.Persistencia.Configuraciones;

/// <summary>Mapeo de <see cref="Usuario"/>.</summary>
public class UsuarioConfiguracion : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> builder)
    {
        builder.ToTable("Usuarios");
        builder.HasKey(u => u.Id);

        builder.Property(u => u.Login)
            .HasMaxLength(50)
            .IsRequired();

        // Garantía por motor de que no hay dos logins iguales.
        builder.HasIndex(u => u.Login)
            .IsUnique()
            .HasDatabaseName("IX_Usuarios_Login");

        builder.Property(u => u.Nombre)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(u => u.Apellidos)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(u => u.Telefono)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(u => u.PasswordHash)
            .HasMaxLength(500)
            .IsRequired();

        // Los enums se guardan como int para que sean estables y comparables.
        builder.Property(u => u.Rol)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(u => u.DebeCambiarPassword).IsRequired();
        builder.Property(u => u.Activo).IsRequired();

        // NombreCompleto es una propiedad calculada: no se persiste.
        builder.Ignore(u => u.NombreCompleto);
    }
}
