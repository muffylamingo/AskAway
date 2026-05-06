namespace AskAway.Models
{
    public class Room
    {
        public int Id { get; set; }
        public string RoomCode { get; set; } // Oyuncuların girmesi için 4 haneli kod (Örn: "X7Y2")
        public string CurrentState { get; set; } = "Lobby"; // Odanın durumu: "Lobby" (Bekleme), "Playing" (Oynanıyor), "Results" (Sonuçlar)

        public int WinningScore { get; set; } = 50; // Hedef puan
        public int? KingPlayerId { get; set; } // Bu turda "Kral" olan (soruyu asıl cevaplayacak) oyuncunun Id'si
        public int? CurrentQuestionId { get; set; } // O an ekranda olan sorunun Id'si

        public int CurrentKingIndex { get; set; } = 0; // Sıranın kimde olduğunu tutar

        // İlişkiler: Bir odanın içinde birden fazla oyuncu vardır
        public ICollection<Player> Players { get; set; } = new List<Player>();

        public int CurrentRound { get; set; } = 1; // Her oda 1. turdan başlar

        public DateTime LastActivity { get; set; } = DateTime.UtcNow;
    }
}