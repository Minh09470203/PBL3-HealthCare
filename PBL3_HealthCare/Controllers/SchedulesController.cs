using Microsoft.AspNetCore.Authorization; // Nhớ có using này
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PBL3_HealthCare.Data;
using PBL3_HealthCare.Models;
using System.Linq;
using System.Threading.Tasks;

namespace PBL3_HealthCare.Controllers
{
    // 1. KHÓA CỬA TỔNG: Phải đăng nhập, và phải là Admin hoặc Bác sĩ mới được vào
    [Authorize(Roles = "Admin, Doctor")]
    public class SchedulesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public SchedulesController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // ==========================================
        // KHU VỰC 1: XEM DANH SÁCH (Ai cũng được xem)
        // ==========================================

        // GET: Schedules
        public async Task<IActionResult> Index()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var isDoctor = await _userManager.IsInRoleAsync(currentUser, "Doctor");

            var query = _context.Schedules
                .Include(s => s.Doctor)
                    .ThenInclude(d => d.User)
                .AsQueryable();

            // Nếu là Bác sĩ -> Ép query chỉ lấy lịch của chính mình
            if (isDoctor)
            {
                var doctor = await _context.Doctors.FirstOrDefaultAsync(d => d.UserId == currentUser.Id);
                if (doctor != null)
                {
                    query = query.Where(s => s.DoctorId == doctor.Id);
                }
            }

            var schedules = await query.OrderByDescending(s => s.Date).ToListAsync();
            return View(schedules);
        }

        // GET: Schedules/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var schedule = await _context.Schedules
                .Include(s => s.Doctor).ThenInclude(d => d.User)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (schedule == null) return NotFound();

            return View(schedule);
        }

        // ==========================================
        // KHU VỰC 2: THÊM/SỬA/XÓA (CHỈ DÀNH CHO ADMIN)
        // ==========================================

        // 2. BÙA CHỐNG HACKER: Đóng chặt cửa, chỉ Admin mới được đi xuống dưới
        [Authorize(Roles = "Admin")]
        public IActionResult Create()
        {
            ViewData["DoctorId"] = new SelectList(_context.Doctors.Include(d => d.User), "Id", "User.FullName");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create([Bind("Id,DoctorId,Date,Shift,IsAvailable")] Schedule schedule)
        {
            if (ModelState.IsValid)
            {
                _context.Add(schedule);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Tạo ca làm việc thành công!";
                return RedirectToAction(nameof(Index));
            }
            ViewData["DoctorId"] = new SelectList(_context.Doctors.Include(d => d.User), "Id", "User.FullName", schedule.DoctorId);
            return View(schedule);
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var schedule = await _context.Schedules.FindAsync(id);
            if (schedule == null) return NotFound();

            ViewData["DoctorId"] = new SelectList(_context.Doctors.Include(d => d.User), "Id", "User.FullName", schedule.DoctorId);
            return View(schedule);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(int id, [Bind("Id,DoctorId,Date,Shift,IsAvailable")] Schedule schedule)
        {
            if (id != schedule.Id) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(schedule);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Cập nhật thành công!";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ScheduleExists(schedule.Id)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }
            ViewData["DoctorId"] = new SelectList(_context.Doctors.Include(d => d.User), "Id", "User.FullName", schedule.DoctorId);
            return View(schedule);
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var schedule = await _context.Schedules
                .Include(s => s.Doctor).ThenInclude(d => d.User)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (schedule == null) return NotFound();

            return View(schedule);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var schedule = await _context.Schedules.FindAsync(id);
            if (schedule != null) _context.Schedules.Remove(schedule);

            await _context.SaveChangesAsync();
            TempData["Success"] = "Xóa thành công!";
            return RedirectToAction(nameof(Index));
        }

        private bool ScheduleExists(int id) => _context.Schedules.Any(e => e.Id == id);
    }
}