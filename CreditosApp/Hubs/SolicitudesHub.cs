using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace CreditosApp.Hubs;

[Authorize]
public class SolicitudesHub : Hub
{
}