namespace PBL3_HealthCare.Models
{
    public class HealthPackage
    {
        public int Id { get; set; }
        public string Name { get; set; } // VD: Tầm soát ung thư
        public string Description { get; set; }
        public decimal Price { get; set; }
        public string ImageUrl { get; set; }
    }
}
