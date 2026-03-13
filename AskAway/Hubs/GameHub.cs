using AskAway.Data;
using AskAway.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace AskAway.Hubs
{
    // Hub sınıfından miras alıyoruz, bu sayede SignalR yetenekleri kazanıyor
    public class GameHub : Hub
    {
        private readonly ApplicationDbContext _context;

        // Veritabanı (DbContext) tercümanımızı buraya çağırıyoruz
        public GameHub(ApplicationDbContext context)
        {
            _context = context;
        }

        // 1. ODA KURMA METODU
        public async Task CreateRoom(string playerName)
        {
            // Rastgele 4 haneli kod üret (Örn: "A7B2")
            string roomCode = Guid.NewGuid().ToString().Substring(0, 4).ToUpper();

            // Odayı veritabanına kaydet
            var room = new Room { RoomCode = roomCode, CurrentState = "Lobby" };
            _context.Rooms.Add(room);
            await _context.SaveChangesAsync();

            // Kuran kişiyi "Host" (Kral değil, oda sahibi) olarak kaydet
            var player = new Player
            {
                Name = playerName,
                ConnectionId = Context.ConnectionId, // Tarayıcı kimliği
                IsHost = true,
                RoomId = room.Id
            };
            _context.Players.Add(player);
            await _context.SaveChangesAsync();

            // Oyuncuyu SignalR grubuna (odaya) ekle
            await Groups.AddToGroupAsync(Context.ConnectionId, roomCode);

            // Oyuncunun ekranına (Frontend'e) oda kodunu gönder
            await Clients.Caller.SendAsync("RoomCreated", roomCode);
        }

        // 2. ODAYA KATILMA METODU
        public async Task JoinRoom(string roomCode, string playerName)
        {
            var room = await _context.Rooms.FirstOrDefaultAsync(r => r.RoomCode == roomCode);

            if (room == null)
            {
                await Clients.Caller.SendAsync("Error", "Böyle bir oda bulunamadı!");
                return;
            }

            var player = new Player
            {
                Name = playerName,
                ConnectionId = Context.ConnectionId,
                IsHost = false,
                RoomId = room.Id
            };
            _context.Players.Add(player);
            await _context.SaveChangesAsync();

            await Groups.AddToGroupAsync(Context.ConnectionId, roomCode);

            // DEĞİŞEN KISIM BURASI: Odadaki tüm oyuncuların güncel listesini çekiyoruz
            var playerList = await _context.Players
                .Where(p => p.RoomId == room.Id)
                .Select(p => new { p.Name, p.IsHost })
                .ToListAsync();

            // Sadece katılanın adını değil, tüm listeyi herkese gönderiyoruz
            await Clients.Group(roomCode).SendAsync("UpdatePlayerList", playerList);
        }

        // 3. OYUNU BAŞLATMA METODU
        public async Task StartGame(string roomCode)
        {
            // Odayı ve içindeki oyuncuları bul
            var room = await _context.Rooms.Include(r => r.Players).FirstOrDefaultAsync(r => r.RoomCode == roomCode);
            if (room == null || !room.Players.Any()) return;

            // --- 1. OYUN SIFIRLAMA MANTIĞI (YENİ EKLEDİĞİMİZ KISIM) ---
            // Eğer birisi hedef puana ulaşmışsa (Oyun Bittiyse) puanları ve kral sırasını sıfırla
            if (room.Players.Any(p => p.Score >= room.WinningScore))
            {
                foreach (var p in room.Players)
                {
                    p.Score = 0;
                }
                room.CurrentKingIndex = 0; // Sırayı başa sar
            }

            // Her yeni turda (veya yeniden başlarken) eski cevapları temizle
            foreach (var p in room.Players)
            {
                p.CurrentAnswer = null;
            }
            await _context.SaveChangesAsync();
            // -------------------------------------------------------

            // 2. Veritabanında hiç soru yoksa test soruları ekle
            if (!_context.Questions.Any())
            {
                _context.Questions.AddRange(
                    new Question { Text = "Kral ıssız bir adaya düşse yanına alacağı ilk şey ne olurdu?", OptionA = "Telefon", OptionB = "Bıçak", OptionC = "Kitap", OptionD = "Güneş Kremi", OptionE = "Uyku Tulumu" },
                    new Question { Text = "Kral bir süper kahraman olsaydı gücü ne olurdu?", OptionA = "Görünmezlik", OptionB = "Uçma", OptionC = "Zihin Okuma", OptionD = "Işınlanma", OptionE = "Zamanı Durdurma" }
                );
                await _context.SaveChangesAsync();
            }

            // 3. Rastgele bir soru seç
            var question = await _context.Questions.OrderBy(q => Guid.NewGuid()).FirstOrDefaultAsync();

            // 4. SIRALI KRAL SEÇİMİ
            var players = room.Players.OrderBy(p => p.Id).ToList();
            var king = players[room.CurrentKingIndex % players.Count];

            // Bir sonraki tur için indeksi 1 artır
            room.CurrentKingIndex++;

            // Oyun durumunu güncelle
            room.CurrentState = "Playing";
            room.KingPlayerId = king.Id;
            room.CurrentQuestionId = question.Id;
            await _context.SaveChangesAsync();

            // Ekranlara gönderilecek veriyi hazırla
            var gameData = new
            {
                kingName = king.Name,
                questionText = question.Text,
                options = new[] { question.OptionA, question.OptionB, question.OptionC, question.OptionD, question.OptionE }
            };

            // Herkese oyunun başladığını haber ver
            await Clients.Group(roomCode).SendAsync("GameStarted", gameData);
        }
        // 4. CEVAP GÖNDERME METODU
        public async Task SubmitAnswer(string roomCode, string playerName, string selectedOption)
        {
            // 1. Odayı ve oyuncuları veritabanından getiriyoruz
            var room = await _context.Rooms
                .Include(r => r.Players)
                .FirstOrDefaultAsync(r => r.RoomCode == roomCode);

            if (room == null) return;

            // 2. Cevap veren oyuncuyu bulup seçtiği şıkkı kaydediyoruz
            var player = room.Players.FirstOrDefault(p => p.Name == playerName);
            if (player != null)
            {
                player.CurrentAnswer = selectedOption;
                await _context.SaveChangesAsync();
            }

            // 3. Odadaki toplam ve cevap veren oyuncu sayısını hesaplıyoruz
            int totalPlayers = room.Players.Count;
            int answeredPlayers = room.Players.Count(p => !string.IsNullOrEmpty(p.CurrentAnswer));

            // Ekranlardaki "1/2 answered" yazısını güncelliyoruz
            await Clients.Group(roomCode).SendAsync("UpdateAnswerCount", answeredPlayers, totalPlayers);

            // --- PUANLAMA BURADA BAŞLIYOR: Herkes cevap verdiyse ---
            if (answeredPlayers == totalPlayers)
            {
                // Kralın kim olduğunu ve ne cevap verdiğini buluyoruz
                var king = room.Players.First(p => p.Id == room.KingPlayerId);
                string kingAnswer = king.CurrentAnswer;

                var roundResults = new List<object>();

                foreach (var p in room.Players)
                {
                    bool isCorrect = false;

                    // MANTIK: Eğer oyuncu Kral değilse ve seçtiği şık Kral'ınkiyle aynıysa +5 puan ver
                    if (p.Id != king.Id && p.CurrentAnswer == kingAnswer)
                    {
                        p.Score += 5; // Burası puanın eklendiği yer
                        isCorrect = true;
                    }

                    // Her oyuncunun durumunu sonuç listesine ekle
                    roundResults.Add(new
                    {
                        playerName = p.Name,
                        isKing = p.Id == king.Id,
                        answer = p.CurrentAnswer,
                        isCorrect = isCorrect,
                        score = p.Score
                    });
                }

                // Biri 50 puana ulaştıysa oyun biter
                bool isGameOver = room.Players.Any(p => p.Score >= room.WinningScore);

                // Değişiklikleri veritabanına kaydet
                await _context.SaveChangesAsync();

                // Frontend'e (lobby.js) tüm sonuçları ve oyunun bitip bitmediğini gönder
                await Clients.Group(roomCode).SendAsync("ShowResults", new
                {
                    correctAnswer = kingAnswer,
                    playerResults = roundResults.OrderByDescending(r => (int)r.GetType().GetProperty("score").GetValue(r)).ToList(),
                    isGameOver = isGameOver
                });
            }
        }
    }
}