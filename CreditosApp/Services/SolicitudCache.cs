using CreditosApp.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;

namespace CreditosApp.Services;

public class SolicitudCache
{
    public const string Prefijo = "solicitudes_user_";

    private readonly ApplicationDbContext _db;
    private readonly IDistributedCache _cache;

    public SolicitudCache(ApplicationDbContext db, IDistributedCache cache)
    {
        _db = db;
        _cache = cache;
    }

    public string Clave(string usuarioId) => $"{Prefijo}{usuarioId}";

    public async Task InvalidarAsync(string usuarioId)
    {
        if (string.IsNullOrEmpty(usuarioId))
        {
            return;
        }

        await _cache.RemoveAsync(Clave(usuarioId));
    }

    public async Task InvalidarPorSolicitudAsync(int solicitudId)
    {
        var usuarioId = await (from s in _db.SolicitudesCredito
                               join c in _db.Clientes on s.ClienteId equals c.Id
                               where s.Id == solicitudId
                               select c.UsuarioId).FirstOrDefaultAsync();

        if (!string.IsNullOrEmpty(usuarioId))
        {
            await InvalidarAsync(usuarioId);
        }
    }
}