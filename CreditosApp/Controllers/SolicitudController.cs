using System.Security.Claims;
using System.Text.Json;
using CreditosApp.Data;
using CreditosApp.Models;
using CreditosApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;

namespace CreditosApp.Controllers;

[Authorize]
public class SolicitudController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IDistributedCache _cache;
    private readonly SolicitudCache _cacheService;
    private readonly IRabbitMQService _rabbitMq;

    public SolicitudController(ApplicationDbContext db, IDistributedCache cache, SolicitudCache cacheService, IRabbitMQService rabbitMq)
    {
        _db = db;
        _cache = cache;
        _cacheService = cacheService;
        _rabbitMq = rabbitMq;
    }

    [HttpGet]
    public async Task<IActionResult> MisSolicitudes(SolicitudFiltro Filtro)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var solicitudes = await ObtenerSolicitudesUsuarioAsync(userId);

        if (!ValidarFiltros(Filtro, ModelState))
        {
            return View(CrearViewModel(Filtro, solicitudes));
        }

        IEnumerable<SolicitudViewModel> resultados = solicitudes;

        if (Filtro.Estado.HasValue)
        {
            resultados = resultados.Where(s => s.Estado == Filtro.Estado.Value);
        }

        if (Filtro.MontoMinimo.HasValue)
        {
            resultados = resultados.Where(s => s.MontoSolicitado >= Filtro.MontoMinimo.Value);
        }

        if (Filtro.MontoMaximo.HasValue)
        {
            resultados = resultados.Where(s => s.MontoSolicitado <= Filtro.MontoMaximo.Value);
        }

        if (Filtro.FechaInicio.HasValue)
        {
            var inicio = Filtro.FechaInicio.Value.Date;
            resultados = resultados.Where(s => s.FechaSolicitud >= inicio);
        }

        if (Filtro.FechaFin.HasValue)
        {
            var fin = Filtro.FechaFin.Value.Date.AddDays(1);
            resultados = resultados.Where(s => s.FechaSolicitud < fin);
        }

        return View(CrearViewModel(Filtro, resultados.ToList()));
    }

    [HttpGet]
    public async Task<IActionResult> Detalle(int id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        var solicitud = await (from s in _db.SolicitudesCredito
                               join c in _db.Clientes on s.ClienteId equals c.Id
                               join u in _db.Users on c.UsuarioId equals u.Id
                               where s.Id == id && u.Id == userId
                               select new SolicitudViewModel
                               {
                                   Id = s.Id,
                                   ClienteId = c.Id,
                                   EmailCliente = u.Email ?? "",
                                   IngresosMensuales = c.IngresosMensuales,
                                   MontoSolicitado = s.MontoSolicitado,
                                   FechaSolicitud = s.FechaSolicitud,
                                   Estado = s.Estado,
                                   MotivoRechazo = s.MotivoRechazo
                               }).FirstOrDefaultAsync();

        if (solicitud is null)
        {
            return NotFound();
        }

        HttpContext.Session.SetString("UltimaSolicitud", $"Monto: {solicitud.MontoSolicitado:C} - ID: {solicitud.Id}");
        HttpContext.Session.SetString("UltimaSolicitudId", solicitud.Id.ToString());

        return View(solicitud);
    }

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> Crear()
    {
        var cliente = await ObtenerClienteAsync(User.FindFirstValue(ClaimTypes.NameIdentifier));

        if (cliente is null)
        {
            TempData["Error"] = "No tienes un perfil de cliente registrado.";
            return RedirectToAction(nameof(MisSolicitudes));
        }

        if (!cliente.Activo)
        {
            TempData["Error"] = "Tu perfil de cliente se encuentra inactivo. No puedes crear solicitudes.";
            return RedirectToAction(nameof(MisSolicitudes));
        }

        return View(RellenarViewModel(new CrearSolicitudViewModel(), cliente));
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Crear([Bind(Prefix = "")] CrearSolicitudViewModel model)
    {
        var cliente = await ObtenerClienteAsync(User.FindFirstValue(ClaimTypes.NameIdentifier));

        if (cliente is null)
        {
            TempData["Error"] = "No tienes un perfil de cliente registrado.";
            return RedirectToAction(nameof(MisSolicitudes));
        }

        if (!cliente.Activo)
        {
            ModelState.AddModelError(string.Empty, "Tu perfil de cliente se encuentra inactivo. No puedes crear solicitudes.");
            return View(RellenarViewModel(model, cliente));
        }

        var tienePendiente = await _db.SolicitudesCredito.AnyAsync(
            s => s.ClienteId == cliente.Id && s.Estado == EstadoSolicitud.Pendiente);

        if (tienePendiente)
        {
            ModelState.AddModelError(string.Empty, "Ya tienes una solicitud de crédito en estado Pendiente.");
        }

        if (model.MontoSolicitado <= 0)
        {
            ModelState.AddModelError(nameof(CrearSolicitudViewModel.MontoSolicitado), "El monto solicitado debe ser mayor a 0.");
        }
        else if (model.MontoSolicitado > cliente.IngresosMensuales * 10)
        {
            ModelState.AddModelError(
                nameof(CrearSolicitudViewModel.MontoSolicitado),
                "El monto solicitado no puede superar 10 veces tus ingresos mensuales.");
        }

        if (!ModelState.IsValid)
        {
            return View(RellenarViewModel(model, cliente));
        }

        var solicitud = new SolicitudCredito
        {
            ClienteId = cliente.Id,
            MontoSolicitado = model.MontoSolicitado,
            FechaSolicitud = DateTime.UtcNow,
            Estado = EstadoSolicitud.Pendiente
        };

        _db.SolicitudesCredito.Add(solicitud);
        await _db.SaveChangesAsync();

        await _cacheService.InvalidarAsync(cliente.UsuarioId);

        var publicado = await _rabbitMq.PublicarSolicitudRegistradaAsync(new SolicitudRegistrada(
            MessageId: Guid.NewGuid().ToString(),
            SolicitudId: solicitud.Id,
            UsuarioId: cliente.UsuarioId,
            FechaEventoUtc: DateTime.UtcNow));

        TempData["Exito"] = "Solicitud de crédito creada correctamente.";

        if (!publicado)
        {
            TempData["Advertencia"] = "La solicitud se guardó correctamente, pero no se pudo enviar la notificación (servicio de mensajería no disponible).";
        }

        return RedirectToAction(nameof(MisSolicitudes));
    }

    private async Task<List<SolicitudViewModel>> ObtenerSolicitudesUsuarioAsync(string? usuarioId)
    {
        if (string.IsNullOrEmpty(usuarioId))
        {
            return new List<SolicitudViewModel>();
        }

        var clave = _cacheService.Clave(usuarioId);

        var bytes = await _cache.GetAsync(clave);
        if (bytes is not null)
        {
            var delCache = JsonSerializer.Deserialize<List<SolicitudViewModel>>(bytes);
            if (delCache is not null)
            {
                return delCache;
            }
        }

        var consulta =
            from s in _db.SolicitudesCredito
            join c in _db.Clientes on s.ClienteId equals c.Id
            join u in _db.Users on c.UsuarioId equals u.Id
            where u.Id == usuarioId
            select s;

        var solicitudes = await ProyectarAsync(consulta);

        await _cache.SetAsync(clave, JsonSerializer.SerializeToUtf8Bytes(solicitudes), new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60)
        });

        return solicitudes;
    }

    private static CrearSolicitudViewModel RellenarViewModel(CrearSolicitudViewModel model, Cliente cliente)
    {
        model.IngresosMensuales = cliente.IngresosMensuales;
        model.LimiteMaximo = cliente.IngresosMensuales * 10;
        return model;
    }

    private Task<Cliente?> ObtenerClienteAsync(string? usuarioId)
    {
        return _db.Clientes.FirstOrDefaultAsync(c => c.UsuarioId == usuarioId);
    }

    private async Task<List<SolicitudViewModel>> ProyectarAsync(IQueryable<SolicitudCredito> consulta)
    {
        var query =
            from s in consulta
            join c in _db.Clientes on s.ClienteId equals c.Id
            join u in _db.Users on c.UsuarioId equals u.Id
            orderby s.FechaSolicitud descending
            select new SolicitudViewModel
            {
                Id = s.Id,
                ClienteId = c.Id,
                EmailCliente = u.Email ?? "",
                IngresosMensuales = c.IngresosMensuales,
                MontoSolicitado = s.MontoSolicitado,
                FechaSolicitud = s.FechaSolicitud,
                Estado = s.Estado,
                MotivoRechazo = s.MotivoRechazo
            };

        return await query.ToListAsync();
    }

    private static MisSolicitudesViewModel CrearViewModel(SolicitudFiltro filtro, List<SolicitudViewModel> solicitudes)
    {
        return new MisSolicitudesViewModel
        {
            Filtro = filtro,
            Solicitudes = solicitudes
        };
    }

    private static bool ValidarFiltros(SolicitudFiltro filtro, ModelStateDictionary modelState)
    {
        var valido = true;
        const string prefijo = "Filtro.";

        if (filtro.MontoMinimo < 0)
        {
            modelState.AddModelError(prefijo + nameof(SolicitudFiltro.MontoMinimo), "El monto mínimo no puede ser negativo.");
            valido = false;
        }

        if (filtro.MontoMaximo < 0)
        {
            modelState.AddModelError(prefijo + nameof(SolicitudFiltro.MontoMaximo), "El monto máximo no puede ser negativo.");
            valido = false;
        }

        if (filtro.MontoMinimo.HasValue && filtro.MontoMaximo.HasValue && filtro.MontoMinimo > filtro.MontoMaximo)
        {
            modelState.AddModelError(string.Empty, "El monto mínimo no puede ser mayor que el monto máximo.");
            valido = false;
        }

        if (filtro.FechaInicio.HasValue && filtro.FechaFin.HasValue && filtro.FechaInicio > filtro.FechaFin)
        {
            modelState.AddModelError(string.Empty, "La fecha de inicio no puede ser posterior a la fecha de fin.");
            valido = false;
        }

        return valido;
    }
}