using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MisCatas.Domain.Entidades;

namespace MisCatas.Infrastructure.Persistencia.Configuraciones;

/// <summary>Mapeo de <see cref="Vino"/>.</summary>
public class VinoConfiguracion : IEntityTypeConfiguration<Vino>
{
    public void Configure(EntityTypeBuilder<Vino> builder)
    {
        builder.ToTable("Vinos");
        builder.HasKey(v => v.Id);

        builder.Property(v => v.Nombre)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(v => v.Clase)
            .HasMaxLength(100);

        builder.Property(v => v.Dueno)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(v => v.Bodega)
            .HasMaxLength(200);

        builder.Property(v => v.DenominacionOrigen)
            .HasMaxLength(150);

        // Precio con dos decimales: suficiente para euros.
        builder.Property(v => v.PrecioEstimado)
            .HasColumnType("decimal(10,2)");

        // Comentarios de longitud libre.
        builder.Property(v => v.Comentarios)
            .HasColumnType("nvarchar(max)");

        // Imágenes ya redimensionadas en el servidor.
        builder.Property(v => v.Foto)
            .HasColumnType("varbinary(max)");

        builder.Property(v => v.FotoContentType)
            .HasMaxLength(100);

        builder.Property(v => v.Etiqueta)
            .HasColumnType("varbinary(max)");

        builder.Property(v => v.EtiquetaContentType)
            .HasMaxLength(100);

        builder.HasIndex(v => v.Nombre)
            .HasDatabaseName("IX_Vinos_Nombre");
    }
}
