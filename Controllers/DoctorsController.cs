using Microsoft.AspNetCore.Authorization;
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
using Microsoft.AspNetCore.Hosting; // Thêm để xài IWebHostEnvironment
using Microsoft.AspNetCore.Http;    // Thêm để xài IFormFile
using System.IO;                    // Thêm để xài Path, FileStream

namespace PBL3_HealthCare.Controllers
{
    // 1. GẮN Ổ KHÓA ADMIN TẠI ĐÂY
    [Authorize(Roles = "Admin")]
    public class DoctorsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IWebHostEnvironment _webHostEnvironment; // Khai báo

        public DoctorsController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _userManager = userManager;
            _webHostEnvironment = webHostEnvironment; // Tiêm vào
        }

        // GET: Doctors
        public async Task<IActionResult> Index()
        {
            var applicationDbContext = _context.Doctors.Include(d => d.Specialty).Include(d => d.User);
            return View(await applicationDbContext.ToListAsync());
        }

        // GET: Doctors/Details/5
        [AllowAnonymous]
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var doctor = await _context.Doctors
                .Include(d => d.Specialty)
                .Include(d => d.User)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (doctor == null) return NotFound();

            return View(doctor);
        }

        // GET: Doctors/Create
        public IActionResult Create()
        {
            ViewData["SpecialtyId"] = new SelectList(_context.Specialties, "Id", "Name");
            ViewData["UserId"] = new SelectList(_context.Users, "Id", "FullName");
            return View();
        }

        // POST: Doctors/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        // BỔ SUNG THÊM THAM SỐ IFormFile AvatarFile
        public async Task<IActionResult> Create([Bind("Id,UserId,SpecialtyId,Bio,Degree,Price,Image,IsVideoAvailable")] Doctor doctor, IFormFile AvatarFile)
        {
            if (ModelState.IsValid)
            {
                // XỬ LÝ UPLOAD ẢNH (NẾU CÓ CHỌN)
                if (AvatarFile != null && AvatarFile.Length > 0)
                {
                    // Tạo thư mục wwwroot/img/doctors nếu chưa có
                    string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "img", "doctors");
                    if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

                    // Đổi tên file để chống trùng lặp
                    string uniqueFileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(AvatarFile.FileName);
                    string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    // Copy file vào ổ cứng
                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await AvatarFile.CopyToAsync(fileStream);
                    }

                    // Lưu đường dẫn vào Model (Ví dụ: "doctors/xyz.jpg")
                    doctor.Image = uniqueFileName;
                }

                // Lưu Bác sĩ vào Database
                _context.Add(doctor);
                await _context.SaveChangesAsync();

                // LOGIC CẤP QUYỀN ĐẶT ĐÚNG CHỖ NÀY
                var user = await _userManager.FindByIdAsync(doctor.UserId);
                if (user != null)
                {
                    if (await _userManager.IsInRoleAsync(user, "Patient"))
                    {
                        await _userManager.RemoveFromRoleAsync(user, "Patient");
                    }
                    await _userManager.AddToRoleAsync(user, "Doctor");
                }

                TempData["Success"] = "Thêm bác sĩ thành công!";
                return RedirectToAction(nameof(Index));
            }
            ViewData["SpecialtyId"] = new SelectList(_context.Specialties, "Id", "Name", doctor.SpecialtyId);
            ViewData["UserId"] = new SelectList(_context.Users, "Id", "FullName", doctor.UserId);
            return View(doctor);
        }

        // GET: Doctors/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var doctor = await _context.Doctors.FindAsync(id);
            if (doctor == null) return NotFound();

            ViewData["SpecialtyId"] = new SelectList(_context.Specialties, "Id", "Name", doctor.SpecialtyId);
            ViewData["UserId"] = new SelectList(_context.Users, "Id", "FullName", doctor.UserId);
            return View(doctor);
        }

        // POST: Doctors/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        // BỔ SUNG THÊM THAM SỐ IFormFile AvatarFile
        public async Task<IActionResult> Edit(int id, [Bind("Id,UserId,SpecialtyId,Bio,Degree,Price,Image,IsVideoAvailable")] Doctor doctor, IFormFile AvatarFile)
        {
            if (id != doctor.Id) return NotFound();

            ModelState.Remove("AvatarFile");
            ModelState.Remove("Image");

            if (ModelState.IsValid)
            {
                try
                {
                    // XỬ LÝ ĐỔI ẢNH MỚI (NẾU CÓ CHỌN FILE)
                    if (AvatarFile != null && AvatarFile.Length > 0)
                    {
                        string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "img", "doctors");
                        if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

                        string uniqueFileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(AvatarFile.FileName);
                        string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                        using (var fileStream = new FileStream(filePath, FileMode.Create))
                        {
                            await AvatarFile.CopyToAsync(fileStream);
                        }

                        // Đè tên file mới vào (tên file cũ được View giữ nguyên nếu không chọn ảnh mới)
                        doctor.Image = uniqueFileName;
                    }

                    _context.Update(doctor);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!DoctorExists(doctor.Id)) return NotFound();
                    else throw;
                }

                TempData["Success"] = "Cập nhật bác sĩ thành công!";
                return RedirectToAction(nameof(Index));
            }
            ViewData["SpecialtyId"] = new SelectList(_context.Specialties, "Id", "Name", doctor.SpecialtyId);
            ViewData["UserId"] = new SelectList(_context.Users, "Id", "FullName", doctor.UserId);
            return View(doctor);
        }

        // GET: Doctors/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var doctor = await _context.Doctors
                .Include(d => d.Specialty)
                .Include(d => d.User)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (doctor == null) return NotFound();

            return View(doctor);
        }

        // POST: Doctors/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var doctor = await _context.Doctors.FindAsync(id);
            if (doctor != null)
            {
                _context.Doctors.Remove(doctor);
            }

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Xóa bác sĩ thành công!";
            return RedirectToAction(nameof(Index));
        }

        private bool DoctorExists(int id)
        {
            return _context.Doctors.Any(e => e.Id == id);
        }

        [AllowAnonymous]
        public async Task<IActionResult> List()
        {
            var allDoctors = await _context.Doctors
                                  .Include(d => d.User)
                                  .Include(d => d.Specialty)
                                  .ToListAsync();

            return View(allDoctors);
        }
    }
}