using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MisCatas.Domain.Entidades;

namespace MisCatas.Infrastructure.Persistencia.Configuraciones;

/// <summary>Mapeo de <see cref="Cata"/>.</summary>
public class CataConfiguracion : IEntityTypeConfiguration<Cata>
{
    public void Configure(EntityTypeBuilder<Cata> builder)
    {
        builder.ToTable("Catas");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Nombre)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(c => c.Lugar)
            .HasMaxLength(200);

        builder.Property(c => c.Fecha)
            .HasColumnType("date")
            .IsRequired();

        builder.Property(c => c.Comentarios)
            .HasColumnType("nvarchar(max)");

        builder.Property(c => c.Estado)
            .HasConversion<int>()
            .IsRequired();

        // Token de concurrencia: SQL Server lo mantiene automáticamente.
        builder.Property(c => c.RowVersion)
            .IsRowVersion();

        // El vino actual es solo un puntero: borrarlo no debe arrastrar la cata.
        builder.HasOne(c => c.VinoActual)
            .WithMany()
            .HasForeignKey(c => c.VinoActualId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.AdminControla)
            .WithMany()
            .HasForeignKey(c => c.AdminControlaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(c => c.Fecha)
            .HasDatabaseName("IX_Catas_Fecha");
    }
}
