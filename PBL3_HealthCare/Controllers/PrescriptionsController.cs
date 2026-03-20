using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PBL3_HealthCare.Data;
using PBL3_HealthCare.Models;
using PBL3_HealthCare.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PBL3_HealthCare.Controllers
{
    [Authorize(Roles = "Doctor, Admin")]
    public class PrescriptionsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public PrescriptionsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Prescriptions
        public async Task<IActionResult> Index()
        {
            var applicationDbContext = _context.Prescriptions.Include(p => p.MedicalRecord);
            return View(await applicationDbContext.ToListAsync());
        }

        // GET: Prescriptions/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var prescription = await _context.Prescriptions
                .Include(p => p.MedicalRecord)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (prescription == null)
            {
                return NotFound();
            }

            return View(prescription);
        }

        // GET: Prescriptions/Create
        [HttpGet]
        public IActionResult Create(int medicalRecordId)
        {
            // Truyền ID cuộc hẹn vào ViewModel
            var model = new CreatePrescriptionVM { MedicalRecordId = medicalRecordId };

            // Gửi danh sách thuốc ra View để FE 1 làm Dropdown chọn thuốc
            ViewBag.Medicines = new SelectList(_context.Medicines.Where(m => m.StockQuantity > 0), "Id", "Name");

            return View(model);
        }

        // ==========================================
        // 2. HÀM POST: XỬ LÝ LƯU ĐƠN THUỐC & TRỪ KHO (TASK 2 + 3)
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreatePrescriptionVM model)
        {
            // Nạp lại list thuốc nếu có lỗi xảy ra để form không bị crash
            ViewBag.Medicines = new SelectList(_context.Medicines.Where(m => m.StockQuantity > 0), "Id", "Name");

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // MỞ TRANSACTION: Đảm bảo "Lưu tất cả hoặc Không lưu gì cả"
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // BƯỚC A: LƯU ĐƠN THUỐC CHA LẤY ID
                var prescription = new Prescription
                {
                    MedicalRecordId = model.MedicalRecordId,
                    Note = model.DoctorNote,
                    PrescriptionDate = DateTime.Now
                };

                _context.Prescriptions.Add(prescription);
                await _context.SaveChangesAsync(); // Chạy Save để EF Core đẻ ra cái prescription.Id

                // BƯỚC B: QUÉT DANH SÁCH THUỐC CON (TRỪ KHO)
                if (model.Details != null && model.Details.Any())
                {
                    foreach (var item in model.Details)
                    {
                        var medicine = await _context.Medicines.FindAsync(item.MedicineId);

                        if (medicine == null) continue;

                        // ---- TASK 3: KIỂM TRA TỒN KHO ----
                        if (medicine.StockQuantity < item.Quantity)
                        {
                            ModelState.AddModelError("", $"Thuốc '{medicine.Name}' chỉ còn {medicine.StockQuantity} viên trong kho, không đủ để kê!");

                            await transaction.RollbackAsync(); // HỦY KÈO, TRẢ LẠI DB NHƯ CŨ
                            return View(model);
                        }

                        // Trừ kho
                        medicine.StockQuantity -= item.Quantity;
                        _context.Medicines.Update(medicine);

                        // Tạo dòng chi tiết đơn thuốc
                        var detail = new PrescriptionDetail
                        {
                            PrescriptionId = prescription.Id,
                            MedicineId = item.MedicineId,
                            Quantity = item.Quantity,
                            Instruction = item.Instruction,
                            UnitPrice = medicine.Price // Chốt giá thuốc ngay tại thời điểm bán
                        };
                        _context.PrescriptionDetails.Add(detail);
                    }
                }

                // BƯỚC C: CHỐT GIAO DỊCH
                await _context.SaveChangesAsync();
                await transaction.CommitAsync(); // Xác nhận lưu vĩnh viễn

                TempData["Success"] = "Kê đơn thuốc và xuất kho thành công!";

                // Kê xong thì đá về trang quản lý lịch khám hoặc chuyển qua cho BE 2 tạo Hóa đơn
                return RedirectToAction("Index", "Appointments");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                ModelState.AddModelError("", "Lỗi hệ thống khi kê đơn: " + ex.Message);
                return View(model);
            }
        }

        // GET: Prescriptions/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var prescription = await _context.Prescriptions.FindAsync(id);
            if (prescription == null)
            {
                return NotFound();
            }
            ViewData["MedicalRecordId"] = new SelectList(_context.MedicalRecords, "Id", "Id", prescription.MedicalRecordId);
            return View(prescription);
        }

        // POST: Prescriptions/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,MedicalRecordId,Note,Status,CreatedDate")] Prescription prescription)
        {
            if (id != prescription.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(prescription);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!PrescriptionExists(prescription.Id))
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
            ViewData["MedicalRecordId"] = new SelectList(_context.MedicalRecords, "Id", "Id", prescription.MedicalRecordId);
            return View(prescription);
        }

        // GET: Prescriptions/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var prescription = await _context.Prescriptions
                .Include(p => p.MedicalRecord)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (prescription == null)
            {
                return NotFound();
            }

            return View(prescription);
        }

        // POST: Prescriptions/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var prescription = await _context.Prescriptions.FindAsync(id);
            if (prescription != null)
            {
                _context.Prescriptions.Remove(prescription);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool PrescriptionExists(int id)
        {
            return _context.Prescriptions.Any(e => e.Id == id);
        }
    }
}
