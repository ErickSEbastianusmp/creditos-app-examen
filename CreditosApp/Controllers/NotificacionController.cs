using System.Security.Claims;
using CreditosApp.Data;
using CreditosApp.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CreditosApp.Controllers;

[Authorize]
public class NotificacionController : Controller
{
    private readonly ApplicationDbContext _db;

    public NotificacionController(ApplicationDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        var notificaciones = await _db.Notificaciones
            .Where(n => n.UsuarioId == userId)
            .OrderByDescending(n => n.FechaProcesamientoUtc)
            .ThenByDescending(n => n.Id)
            .ToListAsync();

        return View(notificaciones);
    }
}