using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace CreditosApp.Models;

[Index(nameof(MessageId), IsUnique = true)]
public class Notificacion
{
    public int Id { get; set; }

    [Required]
    public string MessageId { get; set; } = null!;

    public int SolicitudId { get; set; }

    [Required]
    public string UsuarioId { get; set; } = null!;

    [Required]
    public string Texto { get; set; } = null!;

    public DateTime FechaProcesamientoUtc { get; set; }
}