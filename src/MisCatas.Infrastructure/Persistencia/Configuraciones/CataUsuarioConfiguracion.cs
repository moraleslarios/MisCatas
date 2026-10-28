using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MisCatas.Domain.Entidades;

namespace MisCatas.Infrastructure.Persistencia.Configuraciones;

/// <summary>Mapeo de <see cref="CataUsuario"/> (participantes de una cata).</summary>
public class CataUsuarioConfiguracion : IEntityTypeConfiguration<CataUsuario>
{
    public void Configure(EntityTypeBuilder<CataUsuario> builder)
    {
        builder.ToTable("CataUsuarios");
        builder.HasKey(cu => cu.Id);

        // Un usuario no puede estar invitado dos veces a la misma cata.
        builder.HasIndex(cu => new { cu.CataId, cu.UsuarioId })
            .IsUnique()
            .HasDatabaseName("IX_CataUsuarios_Cata_Usuario");

        // Al borrar la cata desaparecen sus invitaciones.
        builder.HasOne(cu => cu.Cata)
            .WithMany(c => c.Participantes)
            .HasForeignKey(cu => cu.CataId)
            .OnDelete(DeleteBehavior.Cascade);

        // No se permite borrar un usuario que participa en catas.
        builder.HasOne(cu => cu.Usuario)
            .WithMany(u => u.Participaciones)
            .HasForeignKey(cu => cu.UsuarioId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
