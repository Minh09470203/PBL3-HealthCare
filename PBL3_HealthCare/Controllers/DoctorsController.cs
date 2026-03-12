<<<<<<< HEAD
﻿using Microsoft.AspNetCore.Identity;
=======
﻿using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
>>>>>>> 4d46df048740c09244d19a84f1ba3e64e0307fcd
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PBL3_HealthCare.Data;
using PBL3_HealthCare.Models;
using System;
<<<<<<< HEAD
using System.Collections.Generic;
=======
>>>>>>> 4d46df048740c09244d19a84f1ba3e64e0307fcd
using System.Linq;
using System.Threading.Tasks;

namespace PBL3_HealthCare.Controllers
{
    // 1. GẮN Ổ KHÓA ADMIN TẠI ĐÂY
    [Authorize(Roles = "Admin")]
    public class DoctorsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public DoctorsController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }
        // GET: Doctors
        public async Task<IActionResult> Index()
        {
            var applicationDbContext = _context.Doctors.Include(d => d.Specialty).Include(d => d.User);
            return View(await applicationDbContext.ToListAsync());
        }

        // GET: Doctors/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var doctor = await _context.Doctors
                .Include(d => d.Specialty)
                .Include(d => d.User)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (doctor == null)
            {
                return NotFound();
            }

            return View(doctor);
        }

        // GET: Doctors/Create
        public IActionResult Create()
        {
<<<<<<< HEAD
=======
            ViewData["SpecialtyId"] = new SelectList(_context.Specialties, "Id", "Name");
>>>>>>> 4d46df048740c09244d19a84f1ba3e64e0307fcd
            ViewData["UserId"] = new SelectList(_context.Users, "Id", "FullName");
            return View();
        }

        // POST: Doctors/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,UserId,SpecialtyId,Bio,Degree,Price,Image")] Doctor doctor)
        {
            if (ModelState.IsValid)
            {
                // Lưu Bác sĩ vào Database
                _context.Add(doctor);
                await _context.SaveChangesAsync();

                // 2. LOGIC CẤP QUYỀN ĐẶT ĐÚNG CHỖ NÀY
                var user = await _userManager.FindByIdAsync(doctor.UserId);
                if (user != null)
                {
                    if (await _userManager.IsInRoleAsync(user, "Patient"))
                    {
                        await _userManager.RemoveFromRoleAsync(user, "Patient");
                    }
                    await _userManager.AddToRoleAsync(user, "Doctor");
                }

                return RedirectToAction(nameof(Index));
            }
            ViewData["SpecialtyId"] = new SelectList(_context.Specialties, "Id", "Name", doctor.SpecialtyId);
            ViewData["UserId"] = new SelectList(_context.Users, "Id", "FullName", doctor.UserId);
            return View(doctor);
        }

        // GET: Doctors/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var doctor = await _context.Doctors.FindAsync(id);
            if (doctor == null)
            {
                return NotFound();
            }
            ViewData["SpecialtyId"] = new SelectList(_context.Specialties, "Id", "Name", doctor.SpecialtyId);
            ViewData["UserId"] = new SelectList(_context.Users, "Id", "FullName", doctor.UserId);
            return View(doctor);
        }

        // POST: Doctors/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,UserId,SpecialtyId,Bio,Degree,Price,Image")] Doctor doctor)
        {
            if (id != doctor.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    // 3. ĐÃ DỌN SẠCH RÁC Ở ĐÂY, CHỈ GIỮ LẠI LỆNH UPDATE
                    _context.Update(doctor);
                    await _context.SaveChangesAsync();

                    _context.Add(doctor);
                    await _context.SaveChangesAsync();

                    var user = await _userManager.FindByIdAsync(doctor.UserId);

                    if (user != null)
                    {
                        await _userManager.RemoveFromRoleAsync(user, "Patient");
                        await _userManager.AddToRoleAsync(user, "Doctor");
                    }

                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!DoctorExists(doctor.Id))
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
            ViewData["SpecialtyId"] = new SelectList(_context.Specialties, "Id", "Name", doctor.SpecialtyId);
            ViewData["UserId"] = new SelectList(_context.Users, "Id", "FullName", doctor.UserId);
            return View(doctor);
        }

        // GET: Doctors/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var doctor = await _context.Doctors
                .Include(d => d.Specialty)
                .Include(d => d.User)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (doctor == null)
            {
                return NotFound();
            }

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
            return RedirectToAction(nameof(Index));
        }

        private bool DoctorExists(int id)
        {
            return _context.Doctors.Any(e => e.Id == id);
        }
    }
}