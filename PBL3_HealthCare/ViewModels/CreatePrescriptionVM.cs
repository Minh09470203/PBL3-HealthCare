using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace PBL3_HealthCare.ViewModels
{
    // CÁI GIỎ XÁCH TO CHỨA TOÀN BỘ ĐƠN THUỐC
    public class CreatePrescriptionVM
    {
        [Required]
        public int MedicalRecordId { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập chẩn đoán")]
        public string Diagnosis { get; set; } // Chẩn đoán bệnh

        public string DoctorNote { get; set; } // Lời dặn của bác sĩ (Tái khám, kiêng ăn...)

        // MẢNG CHỨA DANH SÁCH THUỐC (FE 1 sẽ dùng Javascript đẻ ra cái này)
        public List<PrescriptionDetailVM> Details { get; set; } = new List<PrescriptionDetailVM>();
    }

    // CÁI TÚI NHỎ CHỨA TỪNG VIÊN THUỐC BÊN TRONG GIỎ
    public class PrescriptionDetailVM
    {
        [Required]
        public int MedicineId { get; set; } // Chọn thuốc gì

        [Required]
        [Range(1, 1000, ErrorMessage = "Số lượng ít nhất là 1")]
        public int Quantity { get; set; } // Số lượng bao nhiêu

        public string Instruction { get; set; } // Cách uống (VD: Sáng 1 viên, tối 1 viên)
    }
}