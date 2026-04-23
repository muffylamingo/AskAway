namespace AskAway.Models
{
    public class Question
    {
        public int Id { get; set; }
        public string Text { get; set; } // Soru metni (Örn: "Kral ıssız bir adaya düşse yanına alacağı ilk şey ne olurdu?")


        public string? OptionA { get; set; }
        public string? OptionB { get; set; }
        public string? OptionC { get; set; }
        public string? OptionD { get; set; }
        public string? OptionE { get; set; }

        // Question.cs modelinin içi
        public int QuestionType { get; set; } = 0; // 0: Klasik Şıklı, 1: Oyuncu Şıklı, 2: Yorum/Açık Uçlu
    }
}