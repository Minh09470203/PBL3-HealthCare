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

namespace PBL3_HealthCare.Controllers
{
    [Authorize(Roles = "Admin")]
    public class SpecialtiesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment; // Thêm

        public SpecialtiesController(ApplicationDbContext context, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment; // Tiêm vào
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
                    string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "img", "specialties");
                    if (!Directory.Exists(uploadsFolder))
                        Directory.CreateDirectory(uploadsFolder);

                    string uniqueFileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(SpecialtyImage.FileName);
                    string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await SpecialtyImage.CopyToAsync(fileStream);
                    }

                    if (!string.IsNullOrEmpty(specialty.Image))
                    {
                        string oldPath = Path.Combine(_webHostEnvironment.WebRootPath, "img", "specialties", specialty.Image);
                        if (System.IO.File.Exists(oldPath))
                            System.IO.File.Delete(oldPath);

                    }

                    specialty.Image = uniqueFileName;
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
                string folder = Path.Combine(_webHostEnvironment.WebRootPath, "img", "specialties");
                if (!Directory.Exists(folder))
                    Directory.CreateDirectory(folder);

                string fileName = Guid.NewGuid() + "_" + Path.GetFileName(SpecialtyImage.FileName);
                string path = Path.Combine(folder, fileName);

                using (var stream = new FileStream(path, FileMode.Create))
                {
                    await SpecialtyImage.CopyToAsync(stream);
                }

                // ❗ xóa ảnh cũ nếu có
                if (!string.IsNullOrEmpty(existing.Image))
                {
                    string oldPath = Path.Combine(folder, existing.Image);
                    if (System.IO.File.Exists(oldPath))
                        System.IO.File.Delete(oldPath);
                }

                existing.Image = fileName;
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
                // Xóa file ảnh khỏi wwwroot nếu có
                if (!string.IsNullOrEmpty(specialty.Image))
                {
                    string imagePath = Path.Combine(_webHostEnvironment.WebRootPath, "img", specialty.Image);
                    if (System.IO.File.Exists(imagePath))
                        System.IO.File.Delete(imagePath);
                }

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