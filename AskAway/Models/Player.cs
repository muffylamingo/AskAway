namespace AskAway.Models
{
    public class Player
    {
        public int Id { get; set; }
        public string Name { get; set; } // Ekranda görünecek isim
        public string? ConnectionId { get; set; } // SignalR bağlantı kimliği (Tarayıcı yenilenince değişir, o yüzden önemli)
        public int Score { get; set; } // Toplam puanı
        public string? CurrentAnswer { get; set; } // O turda verdiği cevap (Sadece "A", "B", "C", "D" veya "E" tutacak)
        public bool IsHost { get; set; } // Odayı kuran ve "Oyunu Başlat" butonuna basacak kişi mi?

        // İlişkiler: Her oyuncu bir odadadır
        public int RoomId { get; set; }
        public Room Room { get; set; }

        public int AnswerOrder { get; set; } = 0; // 0: Henüz cevap vermedi, 1: İlk cevaplayan, vb.
    }
}