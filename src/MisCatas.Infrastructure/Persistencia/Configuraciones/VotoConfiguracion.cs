using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MisCatas.Domain.Entidades;

namespace MisCatas.Infrastructure.Persistencia.Configuraciones;

/// <summary>Mapeo de <see cref="Voto"/>.</summary>
public class VotoConfiguracion : IEntityTypeConfiguration<Voto>
{
    public void Configure(EntityTypeBuilder<Voto> builder)
    {
        builder.ToTable("Votos", tabla => tabla.HasCheckConstraint(
            "CK_Votos_Nota_0_10",
            "[Nota] IS NULL OR ([Nota] >= 0 AND [Nota] <= 10)"));

        builder.HasKey(v => v.Id);

        // Nota de 0,0 a 10,0 con un decimal.
        builder.Property(v => v.Nota)
            .HasColumnType("decimal(4,1)");

        builder.Property(v => v.Comentario)
            .HasColumnType("nvarchar(max)");

        builder.Property(v => v.FechaVotoUtc).IsRequired();
        builder.Property(v => v.SegundosEnVotar).IsRequired();
        builder.Property(v => v.FueRectificado).IsRequired();
        builder.Property(v => v.EsAutomatico).IsRequired();

        builder.Property(v => v.RowVersion)
            .IsRowVersion();

        // La unicidad del voto la impone el motor, no la interfaz.
        builder.HasIndex(v => new { v.CataId, v.VinoId, v.UsuarioId })
            .IsUnique()
            .HasDatabaseName("IX_Votos_Cata_Vino_Usuario");

        // Solo una cascada hacia Votos para evitar múltiples rutas en SQL Server.
        builder.HasOne(v => v.Cata)
            .WithMany(c => c.Votos)
            .HasForeignKey(v => v.CataId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(v => v.Vino)
            .WithMany(vi => vi.Votos)
            .HasForeignKey(v => v.VinoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(v => v.Usuario)
            .WithMany(u => u.Votos)
            .HasForeignKey(v => v.UsuarioId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
