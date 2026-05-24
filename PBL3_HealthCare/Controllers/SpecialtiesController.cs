using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PBL3_HealthCare.Data;
using PBL3_HealthCare.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using PBL3_HealthCare.Services;

namespace PBL3_HealthCare.Controllers
{
    [Authorize(Roles = "Admin")]
    public class SpecialtiesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment; // Thêm
        private readonly CloudinaryService _cloudinaryService;

        public SpecialtiesController(ApplicationDbContext context, IWebHostEnvironment webHostEnvironment, CloudinaryService cloudinaryService)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment; // Tiêm vào
            _cloudinaryService = cloudinaryService;
        }

        // GET: Specialties
        public async Task<IActionResult> Index()
        {
            return View(await _context.Specialties.ToListAsync());
        }

        // GET: Specialties/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var specialty = await _context.Specialties
                .FirstOrDefaultAsync(m => m.Id == id);
            if (specialty == null)
            {
                return NotFound();
            }

            return View(specialty);
        }

        // GET: Specialties/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Specialties/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,Name,Description,Image")] Specialty specialty, IFormFile SpecialtyImage)
        {
            if (ModelState.IsValid)
            {
                // Xử lý upload ảnh nếu có chọn file
                if (SpecialtyImage != null && SpecialtyImage.Length > 0)
                {
                    string imageUrl = await _cloudinaryService.UploadImageAsync(SpecialtyImage);
                    if (!string.IsNullOrEmpty(imageUrl))
                    {
                        specialty.Image = imageUrl;
                    }
                }

                _context.Add(specialty);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Thêm chuyên khoa thành công!";
                return RedirectToAction(nameof(Index));
            }
            return View(specialty);
        }

        // GET: Specialties/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var specialty = await _context.Specialties.FindAsync(id);
            if (specialty == null)
            {
                return NotFound();
            }
            return View(specialty);
        }

        // POST: Specialties/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Specialty specialty, IFormFile? SpecialtyImage)
        {
            if (id != specialty.Id)
                return NotFound();

            // ❗ bỏ validate Image (tránh lỗi ngầm)
            ModelState.Remove("Image");

            if (!ModelState.IsValid)
            {
                return View(specialty);
            }

            var existing = await _context.Specialties.FindAsync(id);
            if (existing == null)
                return NotFound();

            // ✅ Update dữ liệu text
            existing.Name = specialty.Name;
            existing.Description = specialty.Description;

            // ✅ Nếu có ảnh mới thì upload
            if (SpecialtyImage != null && SpecialtyImage.Length > 0)
            {
                string imageUrl = await _cloudinaryService.UploadImageAsync(SpecialtyImage);
                if (!string.IsNullOrEmpty(imageUrl))
                {
                    existing.Image = imageUrl;
                }
            }

            await _context.SaveChangesAsync();

            TempData["Success"] = "Cập nhật thành công!";
            return RedirectToAction(nameof(Index));
        }

        // GET: Specialties/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var specialty = await _context.Specialties
                .FirstOrDefaultAsync(m => m.Id == id);
            if (specialty == null)
            {
                return NotFound();
            }

            return View(specialty);
        }

        // POST: Specialties/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var specialty = await _context.Specialties.FindAsync(id);
            if (specialty != null)
            {
                // Xóa file ảnh khỏi wwwroot nếu có (Đã chuyển sang Cloudinary nên không cần xóa local file nữa, 
                // hoặc nếu muốn tiết kiệm dung lượng mây thì gọi API Cloudinary để xóa, tạm thời bỏ qua)
                _context.Specialties.Remove(specialty);
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = "Xóa chuyên khoa thành công!";
            return RedirectToAction(nameof(Index));
        }

        private bool SpecialtyExists(int id)
        {
            return _context.Specialties.Any(e => e.Id == id);
        }
    }
}