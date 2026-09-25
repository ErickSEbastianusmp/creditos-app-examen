using CreditosApp.Models; // O el namespace exacto donde esté tu modelo SolicitudRegistrada

namespace CreditosApp.Services;

public class NullRabbitMQService : IRabbitMQService
{
    public Task PublicarSolicitudRegistradaAsync(object solicitud)
    {
        // Simulación: Completa la tarea inmediatamente sin enviar nada a un broker externo
        return Task.CompletedTask;
    }
}