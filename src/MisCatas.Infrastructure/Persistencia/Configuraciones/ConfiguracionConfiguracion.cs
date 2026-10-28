using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MisCatas.Domain.Entidades;

namespace MisCatas.Infrastructure.Persistencia.Configuraciones;

/// <summary>Mapeo de <see cref="Configuracion"/> (tabla de una sola fila).</summary>
public class ConfiguracionConfiguracion : IEntityTypeConfiguration<Configuracion>
{
    public void Configure(EntityTypeBuilder<Configuracion> builder)
    {
        builder.ToTable("Configuracion");
        builder.HasKey(c => c.Id);

        // Id fijo: no es identity, la fila única se inserta con Id = 1.
        builder.Property(c => c.Id)
            .ValueGeneratedNever();

        builder.Property(c => c.TiempoVotoSegundos).IsRequired();
        builder.Property(c => c.PorcentajeAlertaRoja).IsRequired();
        builder.Property(c => c.SegundosRevelacionFinal).IsRequired();
        builder.Property(c => c.PermitirAvanzarSinTodosLosVotos).IsRequired();

        builder.Property(c => c.PoliticaNoVoto)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(c => c.NotaPorDefectoNoVoto)
            .HasColumnType("decimal(4,1)");

        builder.Property(c => c.ComentarioAutomaticoNoVoto)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(c => c.PasoDecimalNota)
            .HasColumnType("decimal(3,1)")
            .IsRequired();
    }
}
