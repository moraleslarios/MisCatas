using Microsoft.EntityFrameworkCore;
using MisCatas.Domain.Entidades;

namespace MisCatas.Infrastructure.Persistencia;

/// <summary>
/// Contexto de acceso a datos de la aplicación.
/// En Blazor Server se registra con <c>AddDbContextFactory</c> porque el
/// <see cref="DbContext"/> no es seguro para accesos concurrentes y los
/// componentes interactivos pueden solapar operaciones.
/// </summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> opciones) : base(opciones)
    {
    }

    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Vino> Vinos => Set<Vino>();
    public DbSet<Cata> Catas => Set<Cata>();
    public DbSet<CataUsuario> CataUsuarios => Set<CataUsuario>();
    public DbSet<CataVino> CataVinos => Set<CataVino>();
    public DbSet<Voto> Votos => Set<Voto>();
    public DbSet<Configuracion> Configuraciones => Set<Configuracion>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Carga todas las clases IEntityTypeConfiguration de este ensamblado.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
