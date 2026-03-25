using System.Collections.Generic;   // Cấp phép cho xài chữ List
using PBL3_HealthCare.Models;

namespace PBL3_HealthCare.ViewModels
{
    public class HomeViewModel
    {
        public List<Doctor> TopDoctors { get; set; }
        public List<Doctor> AllDoctors { get; set; }
    }
}
