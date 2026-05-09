using Microsoft.AspNetCore.Authorization;
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
    [Authorize(Roles = "Doctor, Admin")]
    public class MedicalRecordsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public MedicalRecordsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: MedicalRecords
        public async Task<IActionResult> Index()
        {
            var applicationDbContext = _context.MedicalRecords.Include(m => m.Appointment);
            return View(await applicationDbContext.ToListAsync());
        }

        // GET: MedicalRecords/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var medicalRecord = await _context.MedicalRecords
        // 1. Móc Bác sĩ từ Bệnh án (Chuẩn)
        .Include(m => m.Doctor)
            .ThenInclude(d => d.User)
        // 2. Móc Bác sĩ từ Lịch hẹn (Để phòng hờ cho các bệnh án cũ bị lỗi)
        .Include(m => m.Appointment)
            .ThenInclude(a => a.Doctor)
                .ThenInclude(d => d.User)
        // 3. Móc luôn đơn thuốc (để lát nữa in ra nếu bạn cần)
        .Include(m => m.Prescriptions)
            .ThenInclude(p => p.Details)
                .ThenInclude(pd => pd.Medicine)
        .FirstOrDefaultAsync(m => m.Id == id);
            if (medicalRecord == null)
            {
                return NotFound();
            }

            return View(medicalRecord);
        }

        // GET: MedicalRecords/Create
        [Authorize(Roles = "Doctor")]
        public async Task<IActionResult> Create(int? appointmentId)
        {
            // Chốt chặn 1: Không có ID lịch khám thì báo lỗi 404
            if (appointmentId == null) return NotFound();

            // Chốt chặn 2: Tìm Lịch khám kèm theo thông tin Bệnh nhân để hiển thị ở Cột Trái
            var appointment = await _context.Appointments
                .Include(a => a.Patient)
                .Include(a => a.Doctor)
                .FirstOrDefaultAsync(a => a.Id == appointmentId);

            if (appointment == null) return NotFound();

            // Nhét data Lịch khám vào ViewBag để View đọc và in ra Cột Trái
            ViewBag.Appointment = appointment;

            // Trả về View kèm theo 1 Model rỗng đã được mớm sẵn AppointmentId để gài vào Form ẩn (Cột Phải)
            return View(new MedicalRecord { AppointmentId = appointment.Id });
        }

        // POST: MedicalRecords/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Doctor")]
        public async Task<IActionResult> Create([Bind("AppointmentId,Diagnosis,Treatment,ReExaminationDate")] MedicalRecord medicalRecord)
        {
            ModelState.Remove("Doctor");
            ModelState.Remove("ApplicationUser");
            ModelState.Remove("Appointment");
            ModelState.Remove("Prescriptions");

            if (ModelState.IsValid)
            {
                var appointment = await _context.Appointments
                    .Include(a => a.Doctor)
                    .Include(a => a.Patient)
                    .FirstOrDefaultAsync(a => a.Id == medicalRecord.AppointmentId);

                if (appointment != null)
                {
                    // 1. Gán data cho Bệnh án
                    medicalRecord.Doctor = appointment.Doctor;
                    medicalRecord.ApplicationUser = appointment.Patient;
                    medicalRecord.CreatedAt = DateTime.Now;

                    _context.Add(medicalRecord);

                    // 2. 🔥 CHỐT LẬN: Kiểm tra xem đây là ca Dịch vụ hay Khám thường
                    bool isService = appointment.Reason.Contains("Gói Khám") ||
                                     appointment.Reason.Contains("Tại Nhà") ||
                                     appointment.Reason.Contains("Tiêm Chủng");

                    if (isService)
                    {
                        // Nếu là Dịch vụ -> Chốt "Hoàn thành" lịch khám ngay tại đây
                        appointment.Status = AppointmentStatus.Completed;
                        _context.Update(appointment);

                        await _context.SaveChangesAsync();

                        TempData["Success"] = "Lưu bệnh án và hoàn tất ca khám dịch vụ thành công!";
                        return RedirectToAction("Index", "Appointments");
                    }
                    else
                    {
                        // Nếu là Khám thường -> Vẫn đi theo luồng cũ để kê đơn thuốc
                        await _context.SaveChangesAsync();

                        TempData["Success"] = "Lưu bệnh án thành công! Vui lòng kê đơn thuốc.";
                        return RedirectToAction("Create", "Prescriptions", new { medicalRecordId = medicalRecord.Id });
                    }
                }
            }

            // Nếu lỗi Form: Load lại data cho View
            var appointmentData = await _context.Appointments
                .Include(a => a.Patient).Include(a => a.Doctor)
                .FirstOrDefaultAsync(a => a.Id == medicalRecord.AppointmentId);
            ViewBag.Appointment = appointmentData;

            return View(medicalRecord);
        }

        // POST: MedicalRecords/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,AppointmentId,Diagnosis,Treatment,ReExaminationDate,CreatedAt")] MedicalRecord medicalRecord)
        {
            if (id != medicalRecord.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(medicalRecord);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!MedicalRecordExists(medicalRecord.Id))
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
            ViewData["AppointmentId"] = new SelectList(_context.Appointments, "Id", "Id", medicalRecord.AppointmentId);
            return View(medicalRecord);
        }

        // GET: MedicalRecords/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var medicalRecord = await _context.MedicalRecords
                .Include(m => m.Appointment)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (medicalRecord == null)
            {
                return NotFound();
            }

            return View(medicalRecord);
        }

        // POST: MedicalRecords/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var medicalRecord = await _context.MedicalRecords.FindAsync(id);
            if (medicalRecord != null)
            {
                _context.MedicalRecords.Remove(medicalRecord);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool MedicalRecordExists(int id)
        {
            return _context.MedicalRecords.Any(e => e.Id == id);
        }
    }
}
