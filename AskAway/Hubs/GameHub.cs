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
            // 🌟 .Include(r => r.Players) ekleyerek oyuncuları da beraberinde çekiyoruz
            var room = await _context.Rooms
                .Include(r => r.Players)
                .FirstOrDefaultAsync(r => r.RoomCode == roomCode);

            if (room == null)
            {
                await Clients.Caller.SendAsync("Error", "Böyle bir oda bulunamadı!");
                return;
            }

            if (room.Players.Count >= 6)
            {
                await Clients.Caller.SendAsync("Error", "Oda dolu! Maksimum 6 oyuncu katılabilir.");
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

            // --- 1. OYUN SIFIRLAMA VE TEMİZLİK ---
            if (room.Players.Any(p => p.Score >= room.WinningScore))
            {
                foreach (var p in room.Players) { p.Score = 0; }
                room.CurrentKingIndex = 0;
                room.CurrentRound = 1; // Oyun tamamen bittiyse turu da 1'den başlat
            }

            foreach (var p in room.Players)
            {
                p.CurrentAnswer = null;
                p.AnswerOrder = 0;
            }
            await _context.SaveChangesAsync();

            // 2. TEST SORULARI EKLEME (Eğer veritabanı boşsa)
            // Burası aynı kalabilir, yukarıda konuştuğumuz gibi farklı tiplerde soru ekleyebilirsin.

            // --- 3. TUR MANTIĞINA GÖRE SORU SEÇİMİ (GÜNCELLENEN KISIM) ---
            // Tur sayısına göre hangi modda olduğumuzu buluyoruz (0, 1 veya 2)
            int targetMode = (room.CurrentRound - 1) % 3;

            var question = await _context.Questions
                .Where(q => q.QuestionType == targetMode)
                .OrderBy(q => Guid.NewGuid())
                .FirstOrDefaultAsync();

            // Eğer o modda hiç soru yoksa, herhangi bir soru getir (Oyunun kitlenmemesi için)
            if (question == null)
            {
                question = await _context.Questions.OrderBy(q => Guid.NewGuid()).FirstOrDefaultAsync();
            }

            // --- 4. SIRALI KRAL SEÇİMİ ---
            var playersList = room.Players.OrderBy(p => p.Id).ToList();
            var king = playersList[room.CurrentKingIndex % playersList.Count];

            // Bir sonraki tur hazırlıkları
            room.CurrentKingIndex++;
            room.CurrentRound++; // Turu bir artırıyoruz
            room.CurrentState = "Playing";
            room.KingPlayerId = king.Id;
            room.CurrentQuestionId = question.Id;
            await _context.SaveChangesAsync();

            // --- 5. MODA GÖRE ŞIKLARI HAZIRLA (GÜNCELLENEN KISIM) ---
            object finalOptions;

            // Eğer soru tipi 1 ise (Hedef Oyuncu), şıklar odadaki insanların isimleri olmalı
            if (question.QuestionType == 1)
            {
                // ID yerine doğrudan İSİM üzerinden filtreleme yapalım (En güvenlisi)
                var otherPlayers = room.Players
                    .Where(p => p.Name != king.Name)
                    .Select(p => p.Name)
                    .ToList();

                finalOptions = otherPlayers;
            }
            else
            {
                // Klasik modda veritabanındaki şıkları kullan
                finalOptions = new[] { question.OptionA, question.OptionB, question.OptionC, question.OptionD, question.OptionE };
            }

            var gameData = new
            {
                kingName = king.Name,
                questionText = question.Text,
                options = finalOptions, // Hazırladığımız dinamik şıklar
                questionType = question.QuestionType // Frontend'in (JS) hangi modda olduğumuzu bilmesi için şart
            };

            await Clients.Group(roomCode).SendAsync("GameStarted", gameData);
        }
        // 4. CEVAP GÖNDERME METODU
        public async Task SubmitAnswer(string roomCode, string playerName, string selectedOption)
        {
            // 1. Odayı, oyuncuları ve o anki soruyu veritabanından getiriyoruz
            var room = await _context.Rooms
                .Include(r => r.Players)
                .FirstOrDefaultAsync(r => r.RoomCode == roomCode);

            if (room == null) return;

            var player = room.Players.FirstOrDefault(p => p.Name == playerName);
            var currentQuestion = await _context.Questions.FindAsync(room.CurrentQuestionId);
            if (player == null || currentQuestion == null) return;

            var king = room.Players.First(p => p.Id == room.KingPlayerId);

            // 🌟 ŞALTER: Şu an Mod 2'de (Yorum) miyiz yoksa diğerlerinde mi?
            bool isMod2 = currentQuestion.QuestionType == 2;
            bool isKing = player.Id == king.Id;

            // 2. Cevap veren oyuncuyu kaydediyoruz
            if (string.IsNullOrEmpty(player.CurrentAnswer))
            {
                int currentOrder = room.Players.Count(p => !string.IsNullOrEmpty(p.CurrentAnswer)) + 1;
                player.CurrentAnswer = selectedOption;
                player.AnswerOrder = currentOrder;
                await _context.SaveChangesAsync();
            }

            int totalPlayers = room.Players.Count;
            int answeredPlayers = room.Players.Count(p => !string.IsNullOrEmpty(p.CurrentAnswer));

            // ====================================================================
            // 🌟🌟 MOD 2 (YORUM) İÇİN ÇİFT AŞAMALI ÖZEL KURALLAR 🌟🌟
            // ====================================================================
            if (isMod2)
            {
                if (isKing)
                {
                    // AŞAMA 3: KRAL SON KARARINI VERDİ VE OYUN BİTİYOR!
                    // Kralın seçtiği cevabı yazan "Kazananı" buluyoruz
                    var winner = room.Players.FirstOrDefault(p => p.Id != king.Id && p.CurrentAnswer == selectedOption);

                    if (winner != null)
                    {
                        winner.Score += 10; // Kralın seçtiği kişiye kocaman 10 Puan!
                    }

                    var roundResults = new List<object>();
                    foreach (var p in room.Players)
                    {
                        roundResults.Add(new
                        {
                            playerName = p.Name,
                            isKing = p.Id == king.Id,
                            answerText = p.Id == king.Id ? "Seçici" : p.CurrentAnswer,
                            isCorrect = (winner != null && p.Id == winner.Id), // Sadece Kralın seçtiği doğru sayılır
                            score = p.Score
                        });
                    }

                    bool isGameOver = room.Players.Any(p => p.Score >= room.WinningScore);
                    await _context.SaveChangesAsync();

                    // Sonuç ekranını herkese yolla
                    await Clients.Group(roomCode).SendAsync("ShowResults", new
                    {
                        correctAnswerLetter = "",
                        correctAnswerContent = selectedOption, // Kralın seçtiği komik yazı
                        playerResults = roundResults.OrderByDescending(r => (int)r.GetType().GetProperty("score").GetValue(r)).ToList(),
                        isGameOver = isGameOver
                    });
                }
                else
                {
                    // AŞAMA 1 & 2: OYUNCULAR YAZI YAZIYOR (Kral beklenmiyor)
                    // Mod 2'de hedef kişi sayısı toplam sayıdan 1 eksiktir (Kral yazmayacağı için)
                    int targetAnswers = totalPlayers - 1;
                    await Clients.Group(roomCode).SendAsync("UpdateAnswerCount", answeredPlayers, targetAnswers);

                    if (answeredPlayers == targetAnswers)
                    {
                        // HERKES YAZDI! Şimdi cevapları toplayıp anonim (isimsiz) şekilde Kral'a yollayalım
                        var anonymousAnswers = room.Players
                            .Where(p => p.Id != king.Id && !string.IsNullOrEmpty(p.CurrentAnswer))
                            .Select(p => p.CurrentAnswer)
                            .OrderBy(a => Guid.NewGuid()) // 🎲 Sırrını bozmamak için cevapları karıştırıyoruz
                            .ToList();

                        // Yeni komut: "KralSeçimEkranınıGöster"
                        await Clients.Group(roomCode).SendAsync("ShowKingSelection", anonymousAnswers);
                    }
                }
            }
            // ====================================================================
            // 🌟🌟 MOD 0 VE 1 İÇİN KLASİK KURALLAR (Eski kodun aynısı) 🌟🌟
            // ====================================================================
            // ====================================================================
            // 🌟🌟 MOD 0 VE 1 İÇİN KLASİK KURALLAR (GÜNCELLENDİ) 🌟🌟
            // ====================================================================
            else
            {
                await Clients.Group(roomCode).SendAsync("UpdateAnswerCount", answeredPlayers, totalPlayers);

                if (answeredPlayers == totalPlayers)
                {
                    string kingLetter = king.CurrentAnswer; // Kralın seçtiği harf (A, B, C...)
                    string kingAnswerText = "";

                    // 🌟 1. KRALIN CEVABINI ÇÖZÜMLE 🌟
                    if (currentQuestion.QuestionType == 1)
                    {
                        // Mod 1: Şıklar veritabanından DEĞİL, oyuncu isimlerinden geliyor!
                        var dynamicOptions = room.Players.Where(p => p.Name != king.Name).Select(p => p.Name).ToList();

                        // Harfi (A, B, C) sıraya (0, 1, 2) çeviriyoruz
                        int kingIndex = kingLetter switch { "A" => 0, "B" => 1, "C" => 2, "D" => 3, "E" => 4, _ => -1 };

                        kingAnswerText = (kingIndex >= 0 && kingIndex < dynamicOptions.Count)
                            ? dynamicOptions[kingIndex]
                            : kingLetter;
                    }
                    else
                    {
                        // Mod 0: Klasik veritabanı şıkları
                        kingAnswerText = kingLetter switch
                        {
                            "A" => currentQuestion.OptionA,
                            "B" => currentQuestion.OptionB,
                            "C" => currentQuestion.OptionC,
                            "D" => currentQuestion.OptionD,
                            "E" => currentQuestion.OptionE,
                            _ => "Bilinmeyen Şık"
                        };
                    }

                    // Puanları dağıt
                    var correctPlayers = room.Players
                        .Where(p => p.Id != king.Id && p.CurrentAnswer == kingLetter)
                        .OrderBy(p => p.AnswerOrder)
                        .ToList();

                    for (int i = 0; i < correctPlayers.Count; i++)
                    {
                        var p = correctPlayers[i];
                        int speedBonus = 0;
                        if (i == 0) speedBonus = 3;
                        else if (i == 1) speedBonus = 2;
                        p.Score += (5 + speedBonus);
                    }

                    // 🌟 2. OYUNCULARIN CEVAPLARINI ÇÖZÜMLE 🌟
                    var roundResults = new List<object>();
                    foreach (var p in room.Players)
                    {
                        bool isCorrect = (p.Id != king.Id && p.CurrentAnswer == kingLetter);
                        string playerAnswerText = "";

                        if (!string.IsNullOrEmpty(p.CurrentAnswer))
                        {
                            if (currentQuestion.QuestionType == 1)
                            {
                                var dynamicOptions = room.Players.Where(pl => pl.Name != king.Name).Select(pl => pl.Name).ToList();
                                int pIndex = p.CurrentAnswer switch { "A" => 0, "B" => 1, "C" => 2, "D" => 3, "E" => 4, _ => -1 };
                                playerAnswerText = (pIndex >= 0 && pIndex < dynamicOptions.Count) ? dynamicOptions[pIndex] : p.CurrentAnswer;
                            }
                            else
                            {
                                playerAnswerText = p.CurrentAnswer switch
                                {
                                    "A" => currentQuestion.OptionA,
                                    "B" => currentQuestion.OptionB,
                                    "C" => currentQuestion.OptionC,
                                    "D" => currentQuestion.OptionD,
                                    "E" => currentQuestion.OptionE,
                                    _ => "Cevapsız"
                                };
                            }
                        }

                        roundResults.Add(new
                        {
                            playerName = p.Name,
                            isKing = p.Id == king.Id,
                            answerText = playerAnswerText,
                            isCorrect = isCorrect,
                            score = p.Score
                        });
                    }

                    bool isGameOver = room.Players.Any(p => p.Score >= room.WinningScore);
                    await _context.SaveChangesAsync();

                    await Clients.Group(roomCode).SendAsync("ShowResults", new
                    {
                        correctAnswerLetter = kingLetter,
                        correctAnswerContent = kingAnswerText,
                        playerResults = roundResults.OrderByDescending(r => (int)r.GetType().GetProperty("score").GetValue(r)).ToList(),
                        isGameOver = isGameOver
                    });
                }
            }
        }
    }
}