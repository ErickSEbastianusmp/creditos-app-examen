using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.EntityFrameworkCore;
using CreditosApp.Data;
using CreditosApp.Hubs;
using CreditosApp.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

// Opciones de Cache local para evitar dependencias fallidas de Redis en Render
builder.Services.AddDistributedMemoryCache();

builder.Services.AddSession(options =>
{
    options.Cookie.Name = ".CreditosApp.Session";
    options.Cookie.HttpOnly = true;
    options.IdleTimeout = TimeSpan.FromMinutes(20);
});

builder.Services.AddScoped<SolicitudCache>();

// Comentados temporalmente para evitar fallos por falta de servidor RabbitMQ/Redis en Render
// builder.Services.AddSingleton<IRabbitMQService, RabbitMQPublisher>();
// builder.Services.AddHostedService<NotificacionConsumerService>();

builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = true;
});

builder.Services.AddDefaultIdentity<IdentityUser>(options => options.SignIn.RequireConfirmedAccount = true)
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();
builder.Services.AddControllersWithViews();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    await DbInitializer.InitializeAsync(scope.ServiceProvider);
}

// Forzar la pantalla de error detallada de desarrollador
app.UseDeveloperExceptionPage();

app.UseHttpsRedirection();
app.UseRouting();

app.UseWebSockets();

app.UseSession();

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapRazorPages()
   .WithStaticAssets();

app.MapHub<SolicitudesHub>("/hubs/solicitudes");

app.Run();