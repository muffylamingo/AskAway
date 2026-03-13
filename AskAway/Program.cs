using AskAway.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// 1. MVC (Controller ve View) yapýsýný ekliyoruz
builder.Services.AddControllersWithViews();

// 2. Veritabaný (DbContext) ayarýný ekliyoruz
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// 3. SignalR (Gerçek Zamanlý Ýletiþim) servisini ekliyoruz
builder.Services.AddSignalR();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles(); // wwwroot klasöründeki CSS ve JS dosyalarýný okuyabilmek için

app.UseRouting();
app.UseAuthorization();

// 4. Varsayýlan sayfa yönlendirmesi
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapHub<AskAway.Hubs.GameHub>("/gameHub");

app.Run();