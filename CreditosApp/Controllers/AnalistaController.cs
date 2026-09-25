using CreditosApp.Data;
using CreditosApp.Hubs;
using CreditosApp.Models;
using CreditosApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace CreditosApp.Controllers;

[Authorize(Roles = "Analista")]
public class AnalistaController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly SolicitudCache _cacheService;
    private readonly IHubContext<SolicitudesHub> _solicitudesHub;

    public AnalistaController(ApplicationDbContext db, SolicitudCache cacheService, IHubContext<SolicitudesHub> solicitudesHub)
    {
        _db = db;
        _cacheService = cacheService;
        _solicitudesHub = solicitudesHub;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var pendientes = await (from s in _db.SolicitudesCredito
                                join c in _db.Clientes on s.ClienteId equals c.Id
                                join u in _db.Users on c.UsuarioId equals u.Id
                                where s.Estado == EstadoSolicitud.Pendiente
                                orderby s.FechaSolicitud
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
                                }).ToListAsync();

        return View(pendientes);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Aprobar(int id)
    {
        var solicitud = await _db.SolicitudesCredito.FindAsync(id);

        if (solicitud is null)
        {
            return NotFound();
        }

        if (solicitud.Estado != EstadoSolicitud.Pendiente)
        {
            TempData["Error"] = $"La solicitud #{solicitud.Id} ya no está en estado Pendiente.";
            return RedirectToAction(nameof(Index));
        }

        var cliente = await _db.Clientes.FirstOrDefaultAsync(c => c.Id == solicitud.ClienteId);

        if (cliente is null)
        {
            return NotFound();
        }

        var limite = cliente.IngresosMensuales * 5;

        if (solicitud.MontoSolicitado > limite)
        {
            TempData["Error"] =
                $"No se puede aprobar la solicitud #{solicitud.Id}: el monto solicitado ({solicitud.MontoSolicitado:C}) " +
                $"supera 5 veces los ingresos mensuales del cliente ({cliente.IngresosMensuales:C} = {limite:C}).";
            return RedirectToAction(nameof(Index));
        }

        solicitud.Estado = EstadoSolicitud.Aprobado;
        solicitud.MotivoRechazo = null;

        await _db.SaveChangesAsync();
        await _cacheService.InvalidarAsync(cliente.UsuarioId);

        await NotificarClienteAsync(cliente.UsuarioId, solicitud);

        TempData["Exito"] = $"La solicitud #{solicitud.Id} fue aprobada correctamente.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Rechazar(int id, string? motivoRechazo)
    {
        var solicitud = await _db.SolicitudesCredito.FindAsync(id);

        if (solicitud is null)
        {
            return NotFound();
        }

        if (solicitud.Estado != EstadoSolicitud.Pendiente)
        {
            TempData["Error"] = $"La solicitud #{solicitud.Id} ya no está en estado Pendiente.";
            return RedirectToAction(nameof(Index));
        }

        if (string.IsNullOrWhiteSpace(motivoRechazo))
        {
            TempData["Error"] = $"Debes indicar el motivo para rechazar la solicitud #{solicitud.Id}.";
            return RedirectToAction(nameof(Index));
        }

        var cliente = await _db.Clientes.FirstOrDefaultAsync(c => c.Id == solicitud.ClienteId);

        if (cliente is null)
        {
            return NotFound();
        }

        solicitud.Estado = EstadoSolicitud.Rechazado;
        solicitud.MotivoRechazo = motivoRechazo.Trim();

        await _db.SaveChangesAsync();
        await _cacheService.InvalidarAsync(cliente.UsuarioId);

        await NotificarClienteAsync(cliente.UsuarioId, solicitud);

        TempData["Exito"] = $"La solicitud #{solicitud.Id} fue rechazada correctamente.";
        return RedirectToAction(nameof(Index));
    }

    private Task NotificarClienteAsync(string propietarioUsuarioId, SolicitudCredito solicitud)
    {
        return _solicitudesHub.Clients.User(propietarioUsuarioId)
            .SendAsync(
                "SolicitudEstadoActualizado",
                new
                {
                    solicitudId = solicitud.Id,
                    estado = solicitud.Estado.ToString(),
                    motivoRechazo = solicitud.MotivoRechazo
                });
    }
}