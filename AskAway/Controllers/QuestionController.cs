using AskAway.Data;
using AskAway.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AskAway.Controllers
{
    public class QuestionsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public QuestionsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // Tüm soruları listeler
        public async Task<IActionResult> Index()
        {
            var questions = await _context.Questions.ToListAsync();
            return View(questions);
        }

        // Yeni soru ekleme sayfası
        public IActionResult Create()
        {
            return View();
        }

        // Yeni soruyu kaydeder
        [HttpPost]
        [ValidateAntiForgeryToken] // Güvenlik önlemi (CSRF ataklarına karşı korur)
        public async Task<IActionResult> Create(Question question)
        {
            // 🌟 YENİ EKLENEN KISIM: 
            // Eğer soru tipi 1 (Oyuncu) veya 2 (Yorum) ise, 
            // A, B, C, D, E şıklarının boş gelmesine izin ver (Hata fırlatma!)
            if (question.QuestionType == 1 || question.QuestionType == 2)
            {
                ModelState.Remove("OptionA");
                ModelState.Remove("OptionB");
                ModelState.Remove("OptionC");
                ModelState.Remove("OptionD");
                ModelState.Remove("OptionE");
            }

            // Doğrulama başarılıysa veritabanına kaydet
            if (ModelState.IsValid)
            {
                _context.Questions.Add(question);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            // Eğer formda hala hata varsa (örn. sorunun metni boşsa) aynı sayfaya geri dön
            return View(question);
        }

        // Soru silme işlemi
        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var question = await _context.Questions.FindAsync(id);
            if (question != null)
            {
                _context.Questions.Remove(question);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }
    }
}