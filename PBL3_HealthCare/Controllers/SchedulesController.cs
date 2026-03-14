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
    public class SchedulesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public SchedulesController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: Schedules
        public async Task<IActionResult> Index()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var isDoctor = await _userManager.IsInRoleAsync(currentUser, "Doctor");

            var query = _context.Schedules
                .Include(s => s.Doctor)
                    .ThenInclude(d => d.User)
                .AsQueryable();

            if (isDoctor)
            {
                var doctor = await _context.Doctors.FirstOrDefaultAsync(d => d.UserId == currentUser.Id);
                if (doctor != null)
                {
                    query = query.Where(s => s.DoctorId == doctor.Id);
                }
            }

            var applicationDbContext = await query.OrderByDescending(s => s.Date).ToListAsync();
            return View(applicationDbContext);
        }

        // GET: Schedules/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var schedule = await _context.Schedules
                .Include(s => s.Doctor)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (schedule == null)
            {
                return NotFound();
            }

            return View(schedule);
        }

        // GET: Schedules/Create
        public IActionResult Create()
        {
            // Vẫn truyền List Doctor ra để Admin có thể chọn
            ViewData["DoctorId"] = new SelectList(_context.Doctors.Include(d => d.User), "Id", "User.FullName");
            return View();
        }

        // POST: Schedules/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,DoctorId,Date,StartTime,EndTime,IsAvailable")] Schedule schedule)
        {
            if (ModelState.IsValid)
            {
                var currentUser = await _userManager.GetUserAsync(User);
                var isDoctor = await _userManager.IsInRoleAsync(currentUser, "Doctor");

                // BÍ QUYẾT BẢO MẬT: Nếu là Bác sĩ thì ép cứng ID, phớt lờ data từ Form gửi lên
                if (isDoctor)
                {
                    var doctor = await _context.Doctors.FirstOrDefaultAsync(d => d.UserId == currentUser.Id);
                    if (doctor != null)
                    {
                        schedule.DoctorId = doctor.Id;
                    }
                }

                _context.Add(schedule);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Tạo ca làm việc thành công!";
                return RedirectToAction(nameof(Index));
            }
            ViewData["DoctorId"] = new SelectList(_context.Doctors.Include(d => d.User), "Id", "User.FullName", schedule.DoctorId);
            return View(schedule);
        }

        // GET: Schedules/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var schedule = await _context.Schedules.FindAsync(id);
            if (schedule == null)
            {
                return NotFound();
            }
            ViewData["DoctorId"] = new SelectList(_context.Doctors.Include(d => d.User), "Id", "User.FullName", schedule.DoctorId);
            return View(schedule);
        }

        // POST: Schedules/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,DoctorId,Date,StartTime,EndTime,IsAvailable")] Schedule schedule)
        {
            if (id != schedule.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var currentUser = await _userManager.GetUserAsync(User);
                    var isDoctor = await _userManager.IsInRoleAsync(currentUser, "Doctor");

                    if (isDoctor)
                    {
                        var doctor = await _context.Doctors.FirstOrDefaultAsync(d => d.UserId == currentUser.Id);
                        if (doctor != null)
                        {
                            schedule.DoctorId = doctor.Id;
                        }
                    }

                    _context.Update(schedule);
                    await _context.SaveChangesAsync();

                    TempData["Success"] = "Cập nhật ca làm việc thành công!";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ScheduleExists(schedule.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            ViewData["DoctorId"] = new SelectList(_context.Doctors.Include(d => d.User), "Id", "User.FullName", schedule.DoctorId);
            return View(schedule);
        }

        // GET: Schedules/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var schedule = await _context.Schedules
                .Include(s => s.Doctor)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (schedule == null)
            {
                return NotFound();
            }

            return View(schedule);
        }

        // POST: Schedules/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var schedule = await _context.Schedules.FindAsync(id);
            if (schedule != null)
            {
                _context.Schedules.Remove(schedule);
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = "Xóa ca làm việc thành công!";
            return RedirectToAction(nameof(Index));
        }

        private bool ScheduleExists(int id)
        {
            return _context.Schedules.Any(e => e.Id == id);
        }
    }
}