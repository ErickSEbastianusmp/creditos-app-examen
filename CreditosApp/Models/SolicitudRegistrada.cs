namespace CreditosApp.Models;

public record SolicitudRegistrada(
    string MessageId,
    int SolicitudId,
    string UsuarioId,
    DateTime FechaEventoUtc);