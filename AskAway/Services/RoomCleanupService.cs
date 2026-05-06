using AskAway.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

public class RoomCleanupService : BackgroundService
{
    // Background servisler "Singleton" (tekil) olduğu için DbContext'i doğrudan içine alamayız.
    // Bu yüzden bir "Fabrika" (ScopeFactory) alıyoruz.
    private readonly IServiceScopeFactory _scopeFactory;

    public RoomCleanupService(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    // Sistem çalıştığı anda bu metod arka planda tetiklenir ve uygulama kapanana kadar durmaz
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupOldRoomsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Temizlik sırasında hata oluştu: {ex.Message}");
            }

            // Temizliği yaptıktan sonra 1 Saat boyunca uyu (Süreyi buradan ayarlayabilirsin)
            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }
    }

    private async Task CleanupOldRoomsAsync(CancellationToken stoppingToken)
    {
        // Kendi DbContext'imiz için geçici bir alan (Scope) açıyoruz
        using (var scope = _scopeFactory.CreateScope())
        {
            // Kendi veritabanı adını buraya yaz (Örn: GameDbContext)
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
gfutrfutfjgfbmbıohıohuuhıuıuugugugguuygfkgjujgkjgjkgjkgjkgjkj
            // Şu anki saatten 2 saat öncesini bul
            var thresholdDate = DateTime.UtcNow.AddHours(-2);

            // Son aktivitesi 2 saatten daha eski olan odaları seç
            var oldRooms = await dbContext.Rooms
                .Where(r => r.LastActivity < thresholdDate)
                .ToListAsync(stoppingToken);

            if (oldRooms.Any())
            {
                // Odaları sil. 
                // (Eğer SQL ayarlarında ilişki doğru kurulduysa, bu odalara bağlı Players verileri de otomatik silinecektir)
                dbContext.Rooms.RemoveRange(oldRooms);
                await dbContext.SaveChangesAsync(stoppingToken);

                Console.WriteLine($"{oldRooms.Count} adet pasif oda (ve içindeki oyuncular) sistemden temizlendi! Temizlik Saati: {DateTime.Now}");
            }
        }
    }
}