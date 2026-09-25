using System.ComponentModel.DataAnnotations;

namespace CreditosApp.Models;

public class SolicitudViewModel
{
    public int Id { get; set; }

    public int ClienteId { get; set; }

    [Display(Name = "Cliente")]
    public string EmailCliente { get; set; } = "";

    [Display(Name = "Ingresos mensuales")]
    public decimal IngresosMensuales { get; set; }

    [Display(Name = "Monto solicitado")]
    public decimal MontoSolicitado { get; set; }

    [Display(Name = "Fecha de solicitud")]
    public DateTime FechaSolicitud { get; set; }

    [Display(Name = "Estado")]
    public EstadoSolicitud Estado { get; set; }

    [Display(Name = "Motivo de rechazo")]
    public string? MotivoRechazo { get; set; }
}