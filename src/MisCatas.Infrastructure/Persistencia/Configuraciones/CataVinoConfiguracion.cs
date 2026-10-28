using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MisCatas.Domain.Entidades;

namespace MisCatas.Infrastructure.Persistencia.Configuraciones;

/// <summary>Mapeo de <see cref="CataVino"/> (vinos de una cata y su orden).</summary>
public class CataVinoConfiguracion : IEntityTypeConfiguration<CataVino>
{
    public void Configure(EntityTypeBuilder<CataVino> builder)
    {
        builder.ToTable("CataVinos");
        builder.HasKey(cv => cv.Id);

        builder.Property(cv => cv.Orden).IsRequired();

        builder.Property(cv => cv.EstadoVotacion)
            .HasConversion<int>()
            .IsRequired();

        // Un vino no se repite dentro de la misma cata.
        builder.HasIndex(cv => new { cv.CataId, cv.VinoId })
            .IsUnique()
            .HasDatabaseName("IX_CataVinos_Cata_Vino");

        // Dos vinos no pueden ocupar la misma posición en el recorrido.
        builder.HasIndex(cv => new { cv.CataId, cv.Orden })
            .IsUnique()
            .HasDatabaseName("IX_CataVinos_Cata_Orden");

        builder.HasOne(cv => cv.Cata)
            .WithMany(c => c.Vinos)
            .HasForeignKey(cv => cv.CataId)
            .OnDelete(DeleteBehavior.Cascade);

        // No se permite borrar un vino que forma parte de alguna cata.
        builder.HasOne(cv => cv.Vino)
            .WithMany(v => v.Participaciones)
            .HasForeignKey(cv => cv.VinoId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
