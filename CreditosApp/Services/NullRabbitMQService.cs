namespace CreditosApp.Services;

public class NullRabbitMQService : IRabbitMQService
{
    public void PublicarMensaje<T>(string queue, T mensaje)
    {
        // Simulación: No hace nada para evitar fallos por falta de servidor RabbitMQ
    }
}