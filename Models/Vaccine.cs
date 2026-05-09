namespace PBL3_HealthCare.Models
{
    public class Vaccine
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string DiseasePrevented { get; set; } // VD: Dại, Cúm mùa
        public string Manufacturer { get; set; }
        public decimal Price { get; set; }
        public int StockQuantity { get; set; }
    }
}
