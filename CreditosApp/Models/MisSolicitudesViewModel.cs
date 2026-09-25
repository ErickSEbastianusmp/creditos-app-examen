namespace CreditosApp.Models;

public class MisSolicitudesViewModel
{
    public SolicitudFiltro Filtro { get; set; } = new();

    public List<SolicitudViewModel> Solicitudes { get; set; } = new();
}