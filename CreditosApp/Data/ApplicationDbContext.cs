using CreditosApp.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CreditosApp.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext(options)
{
    public DbSet<Cliente> Clientes => Set<Cliente>();

    public DbSet<SolicitudCredito> SolicitudesCredito => Set<SolicitudCredito>();

    public DbSet<Notificacion> Notificaciones => Set<Notificacion>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Cliente>(entity =>
        {
            entity.HasOne<IdentityUser>()
                  .WithMany()
                  .HasForeignKey(c => c.UsuarioId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.ToTable(t => t.HasCheckConstraint(
                "CK_Clientes_IngresosMensuales_Positive",
                "[IngresosMensuales] > 0"));
        });

        builder.Entity<SolicitudCredito>(entity =>
        {
            entity.HasOne(s => s.Cliente)
                  .WithMany(c => c.Solicitudes)
                  .HasForeignKey(s => s.ClienteId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.ToTable(t => t.HasCheckConstraint(
                "CK_SolicitudesCredito_MontoSolicitado_Positive",
                "[MontoSolicitado] > 0"));
        });
    }
}