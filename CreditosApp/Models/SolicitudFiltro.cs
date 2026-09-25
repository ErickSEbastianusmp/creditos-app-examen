using System.ComponentModel.DataAnnotations;

namespace CreditosApp.Models;

public class SolicitudFiltro
{
    [Display(Name = "Estado")]
    public EstadoSolicitud? Estado { get; set; }

    [Display(Name = "Monto mínimo")]
    [Range(0, double.MaxValue, ErrorMessage = "El monto mínimo no puede ser negativo.")]
    public decimal? MontoMinimo { get; set; }

    [Display(Name = "Monto máximo")]
    [Range(0, double.MaxValue, ErrorMessage = "El monto máximo no puede ser negativo.")]
    public decimal? MontoMaximo { get; set; }

    [Display(Name = "Fecha de inicio")]
    [DataType(DataType.Date)]
    public DateTime? FechaInicio { get; set; }

    [Display(Name = "Fecha de fin")]
    [DataType(DataType.Date)]
    public DateTime? FechaFin { get; set; }
}