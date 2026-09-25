using CreditosApp.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CreditosApp.Data;

public static class DbInitializer
{
    public const string RolAnalista = "Analista";
    public const string PasswordAnalista = "Analista123!";
    public const string PasswordCliente = "Cliente123!";

    public static async Task InitializeAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        await db.Database.MigrateAsync();

        await SeedRoleAndAnalistaAsync(roleManager, userManager);

        var cliente1User = await EnsureUserAsync(userManager, "cliente1@creditosapp.com", PasswordCliente);
        var cliente2User = await EnsureUserAsync(userManager, "cliente2@creditosapp.com", PasswordCliente);

        await db.SaveChangesAsync();

        if (!await db.Clientes.AnyAsync())
        {
            await db.Clientes.AddRangeAsync(
                new Cliente
                {
                    UsuarioId = cliente1User.Id,
                    IngresosMensuales = 35000m,
                    Activo = true
                },
                new Cliente
                {
                    UsuarioId = cliente2User.Id,
                    IngresosMensuales = 25000m,
                    Activo = true
                });

            await db.SaveChangesAsync();
        }

        if (!await db.SolicitudesCredito.AnyAsync())
        {
            var clientes = await db.Clientes.OrderBy(c => c.Id).ToListAsync();

            await db.SolicitudesCredito.AddRangeAsync(
                new SolicitudCredito
                {
                    ClienteId = clientes[0].Id,
                    MontoSolicitado = 50000m,
                    FechaSolicitud = DateTime.UtcNow.ToLocalTime(),
                    Estado = EstadoSolicitud.Pendiente
                },
                new SolicitudCredito
                {
                    ClienteId = clientes[1].Id,
                    MontoSolicitado = 30000m,
                    FechaSolicitud = DateTime.UtcNow.ToLocalTime().AddDays(-10),
                    Estado = EstadoSolicitud.Aprobado
                });

            await db.SaveChangesAsync();
        }
    }

    private static async Task SeedRoleAndAnalistaAsync(
        RoleManager<IdentityRole> roleManager,
        UserManager<IdentityUser> userManager)
    {
        if (!await roleManager.RoleExistsAsync(RolAnalista))
        {
            await roleManager.CreateAsync(new IdentityRole(RolAnalista));
        }

        var analista = await userManager.FindByEmailAsync("analista@creditosapp.com");
        if (analista is null)
        {
            analista = new IdentityUser
            {
                UserName = "analista@creditosapp.com",
                Email = "analista@creditosapp.com",
                EmailConfirmed = true
            };

            var result = await userManager.CreateAsync(analista, PasswordAnalista);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(
                    $"No se pudo crear el usuario analista: {string.Join(", ", result.Errors.Select(e => e.Description))}");
            }

            await userManager.AddToRoleAsync(analista, RolAnalista);
        }
        else if (!await userManager.IsInRoleAsync(analista, RolAnalista))
        {
            await userManager.AddToRoleAsync(analista, RolAnalista);
        }
    }

    private static async Task<IdentityUser> EnsureUserAsync(
        UserManager<IdentityUser> userManager,
        string email,
        string password)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            user = new IdentityUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true
            };

            var result = await userManager.CreateAsync(user, password);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(
                    $"No se pudo crear el usuario {email}: {string.Join(", ", result.Errors.Select(e => e.Description))}");
            }
        }

        return user;
    }
}