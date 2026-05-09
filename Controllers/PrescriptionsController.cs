using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PBL3_HealthCare.Data;
using PBL3_HealthCare.Models;
using PBL3_HealthCare.Services;
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
        private readonly InvoiceService _invoiceService;
        public PrescriptionsController(ApplicationDbContext context, InvoiceService invoiceService)
        {
            _context = context;
            _invoiceService = invoiceService;
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
        [Authorize(Roles = "Doctor")]
        public async Task<IActionResult> Create(CreatePrescriptionVM model)
        {
            ViewBag.Medicines = new SelectList(_context.Medicines.Where(m => m.StockQuantity > 0), "Id", "Name");
            if (!ModelState.IsValid) return View(model);

            var strategy = _context.Database.CreateExecutionStrategy();
            bool isSuccess = false;

            // Khai báo 2 biến này ở ngoài để lát nữa tạo Hóa đơn
            int? savedPrescriptionId = null;
            int? appointmentIdForInvoice = null;

            try
            {
                await strategy.ExecuteAsync(async () =>
                {
                    using var transaction = await _context.Database.BeginTransactionAsync();
                    try
                    {
                        // BƯỚC A: LƯU ĐƠN THUỐC CHA
                        var prescription = new Prescription
                        {
                            MedicalRecordId = model.MedicalRecordId,
                            Note = model.DoctorNote,
                            CreatedDate = DateTime.Now
                        };

                        _context.Prescriptions.Add(prescription);
                        await _context.SaveChangesAsync();

                        savedPrescriptionId = prescription.Id; // Lấy ID Đơn thuốc ra ngoài

                        // BƯỚC B: TRỪ KHO VÀ LƯU CHI TIẾT THUỐC
                        if (model.Details != null && model.Details.Any())
                        {
                            foreach (var item in model.Details)
                            {
                                var medicine = await _context.Medicines.FindAsync(item.MedicineId);
                                if (medicine == null) continue;

                                if (medicine.StockQuantity < item.Quantity)
                                {
                                    ModelState.AddModelError("", $"Thuốc '{medicine.Name}' chỉ còn {medicine.StockQuantity} viên!");
                                    throw new InvalidOperationException("OUT_OF_STOCK");
                                }

                                medicine.StockQuantity -= item.Quantity;
                                _context.Medicines.Update(medicine);

                                var detail = new PrescriptionDetail
                                {
                                    PrescriptionId = prescription.Id,
                                    MedicineId = item.MedicineId,
                                    Quantity = item.Quantity,
                                    Instruction = item.Instruction,
                                    UnitPrice = medicine.Price
                                };
                                _context.PrescriptionDetails.Add(detail);
                            }
                        }

                        // BƯỚC C: CHỐT LỊCH KHÁM COMPLETED
                        var medicalRecord = await _context.MedicalRecords.FindAsync(model.MedicalRecordId);
                        if (medicalRecord != null)
                        {
                            var appointment = await _context.Appointments.FindAsync(medicalRecord.AppointmentId);
                            if (appointment != null)
                            {
                                appointment.Status = AppointmentStatus.Completed;
                                _context.Update(appointment);
                                appointmentIdForInvoice = appointment.Id; // Lấy ID Lịch khám ra ngoài
                            }
                        }

                        // LƯU TOÀN BỘ VÀO DB VÀ ĐÓNG GIAO DỊCH LẠI
                        await _context.SaveChangesAsync();
                        await transaction.CommitAsync();

                        isSuccess = true;
                    }
                    catch (Exception)
                    {
                        await transaction.RollbackAsync();
                        throw;
                    }
                });

                // =========================================================================
                // BƯỚC D: TẠO HÓA ĐƠN Ở ĐÂY (Khi DB đã lưu xong xuôi hết)
                // =========================================================================
                if (isSuccess && savedPrescriptionId.HasValue)
                {
                    // 1. Gọi hàm tạo Hóa đơn (Truyền ID Đơn thuốc)
                    await _invoiceService.GenerateInvoiceAsync(savedPrescriptionId.Value);

                    // 2. Đi móc cái Hóa đơn vừa tạo ra để lấy ID nhảy trang
                    var newInvoice = await _context.Invoices
                        .FirstOrDefaultAsync(i => i.AppointmentId == appointmentIdForInvoice);

                    TempData["Success"] = "Kê đơn thuốc và xuất hóa đơn thành công!";

                    if (newInvoice != null)
                    {
                        return RedirectToAction("Print", "Invoices", new { id = newInvoice.Id });
                    }

                    return RedirectToAction("Index", "Appointments");
                }
            }
            catch (InvalidOperationException ex) when (ex.Message == "OUT_OF_STOCK")
            {
                return View(model);
            }
            catch (Exception ex)
            {
                string realError = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                ModelState.AddModelError("", "Lỗi DB thực sự là: " + realError);
            }

            return View(model);
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
