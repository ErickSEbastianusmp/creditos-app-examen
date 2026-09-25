using System.ComponentModel.DataAnnotations;

namespace CreditosApp.Models;

public class CrearSolicitudViewModel
{
    [Display(Name = "Monto solicitado")]
    [Range(0.01, double.MaxValue, ErrorMessage = "El monto solicitado debe ser mayor a 0.")]
    public decimal MontoSolicitado { get; set; }

    [Display(Name = "Ingresos mensuales")]
    public decimal IngresosMensuales { get; set; }

    [Display(Name = "Monto máximo solicitable (10x ingresos)")]
    public decimal LimiteMaximo { get; set; }
}