namespace PBL3_HealthCare.Helpers
{
    public static class ImageHelper
    {
        public static string GetImageUrl(string fileName, string defaultImage = "/img/undraw_profile.svg")
        {
            if (string.IsNullOrEmpty(fileName)) return defaultImage;
            
            // Nếu đã là link Cloudinary do upload mới
            if (fileName.StartsWith("http")) return fileName;

            // Nếu là tên file cũ, trỏ về thư mục PBL3-HealthCare trên Cloudinary
            // Lấy tên file gốc (bỏ đi các tiền tố thư mục cũ như "doctors/" hay "specialties/" nếu có trong DB)
            var cleanFileName = System.IO.Path.GetFileName(fileName);
            return $"https://res.cloudinary.com/ddvrj9vdi/image/upload/PBL3-HealthCare/{cleanFileName}";
        }
    }
}
