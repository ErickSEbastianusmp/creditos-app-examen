using CreditosApp.Models;

namespace CreditosApp.Services;

public class NullRabbitMQService : IRabbitMQService
{
    public Task<bool> PublicarSolicitudRegistradaAsync(SolicitudRegistrada evento)
    {
        // Retorna true simulando que el mensaje fue publicado correctamente
        return Task.FromResult(true);
    }
}