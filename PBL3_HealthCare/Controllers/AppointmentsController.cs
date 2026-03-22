using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PBL3_HealthCare.Data;
using PBL3_HealthCare.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PBL3_HealthCare.Controllers
{
    public class AppointmentsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public AppointmentsController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // ==========================================
        // HÀM HỖ TRỢ: NẠP TÊN THẬT VÀO DROPDOWN
        // ==========================================
        private void PopulateNames(Appointment appointment = null)
        {
            var patients = _context.Users
                .Select(u => new { Id = u.Id, Name = u.FullName ?? u.UserName })
                .ToList();

            var doctors = _context.Doctors
                .Include(d => d.User)
                .Select(d => new { Id = d.Id, Name = "BS. " + d.User.FullName })
                .ToList();

            ViewData["PatientId"] = new SelectList(patients, "Id", "Name", appointment?.PatientId);
            ViewData["DoctorId"] = new SelectList(doctors, "Id", "Name", appointment?.DoctorId);
        }

        // ==========================================
        // INDEX
        // ==========================================
        public async Task<IActionResult> Index()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var isDoctor = await _userManager.IsInRoleAsync(currentUser, "Doctor");

            var query = _context.Appointments
                .Include(a => a.Patient)
                .Include(a => a.Doctor).ThenInclude(d => d.User)
                .AsQueryable();

            if (isDoctor)
            {
                var doctor = await _context.Doctors.FirstOrDefaultAsync(d => d.UserId == currentUser.Id);
                if (doctor != null)
                {
                    query = query.Where(a => a.DoctorId == doctor.Id);
                }
            }

            var applicationDbContext = await query.OrderByDescending(a => a.Date).ToListAsync();
            return View(applicationDbContext);
        }

        // ==========================================
        // DETAILS
        // ==========================================
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var appointment = await _context.Appointments
                .Include(a => a.Patient)
                .Include(a => a.Doctor)
                    .ThenInclude(d => d.User)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (appointment == null) return NotFound();

            return View(appointment);
        }

        // ==========================================
        // CREATE
        // ==========================================
        public IActionResult Create()
        {
            PopulateNames();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,PatientId,DoctorId,Date,Reason,Status,TimeSlot,Symptoms,CreatedAt")] Appointment appointment)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var isDoctor = await _userManager.IsInRoleAsync(currentUser, "Doctor");

            // 🔥 BỔ SUNG: Nếu là Doctor → ép về đúng quyền
            if (isDoctor)
            {
                var doctor = await _context.Doctors.FirstOrDefaultAsync(d => d.UserId == currentUser.Id);
                if (doctor != null)
                {
                    appointment.DoctorId = doctor.Id;
                }

                appointment.Status = AppointmentStatus.Pending;
                appointment.CreatedAt = DateTime.Now;
            }

            if (ModelState.IsValid)
            {
                _context.Add(appointment);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Tạo lịch khám thành công!";
                return RedirectToAction(nameof(Index));
            }

            PopulateNames(appointment);
            return View(appointment);
        }

        // ==========================================
        // EDIT
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var appointment = await _context.Appointments
                .Include(a => a.Patient)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (appointment == null) return NotFound();

            PopulateNames(appointment);
            return View(appointment);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,PatientId,DoctorId,Date,Reason,Status,TimeSlot,Symptoms,CreatedAt")] Appointment appointment)
        {
            if (id != appointment.Id) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(appointment);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Cập nhật lịch khám thành công!";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!AppointmentExists(appointment.Id)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }

            PopulateNames(appointment);
            return View(appointment);
        }

        // ==========================================
        // DELETE
        // ==========================================
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var appointment = await _context.Appointments
                .Include(a => a.Doctor).ThenInclude(d => d.User)
                .Include(a => a.Patient)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (appointment == null) return NotFound();

            return View(appointment);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var appointment = await _context.Appointments.FindAsync(id);
            if (appointment != null)
            {
                _context.Appointments.Remove(appointment);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Xóa lịch khám thành công!";
            }
            return RedirectToAction(nameof(Index));
        }

        // ==========================================
        // UPDATE STATUS (ADMIN + DOCTOR DUYỆT)
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(int id, AppointmentStatus newStatus)
        {
            var appointment = await _context.Appointments.FindAsync(id);
            if (appointment == null) return NotFound();

            var currentUser = await _userManager.GetUserAsync(User);
            var isDoctor = await _userManager.IsInRoleAsync(currentUser, "Doctor");

            if (isDoctor)
            {
                var doctor = await _context.Doctors.FirstOrDefaultAsync(d => d.UserId == currentUser.Id);

                // 🔥 CHỈ ĐƯỢC DUYỆT LỊCH CỦA CHÍNH MÌNH
                if (doctor == null || appointment.DoctorId != doctor.Id)
                    return Unauthorized();

                // 🔥 CHỈ ĐƯỢC CHUYỂN TỪ PENDING
                if (appointment.Status != AppointmentStatus.Pending)
                    return BadRequest();
            }

            appointment.Status = newStatus;
            _context.Update(appointment);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Cập nhật trạng thái thành công!";
            return RedirectToAction(nameof(Index));
        }

        private bool AppointmentExists(int id)
        {
            return _context.Appointments.Any(e => e.Id == id);
        }
    }
}