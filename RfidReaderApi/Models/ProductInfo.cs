namespace RfidReaderApi.Models
{
    public class ProductInfo
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Epc { get; set; }
        public string Status { get; set; }
        public string ImageUrl { get; set; }
        public string NetWeight { get; set; }
        public string Pieces { get; set; }
        public string UnitOfMeasure { get; set; }
        public string PrintCard { get; set; }
        public string Operator { get; set; }
        public string TipoEtiqueta { get; set; }
        public string Area { get; set; }
        public string ClaveProducto { get; set; }
        public string PesoBruto { get; set; }
        public string PesoTarima { get; set; }
        public DateTime? FechaEntrada { get; set; }
        public TimeSpan? HoraEntrada { get; set; }
        public string Rfid { get; set; }
        public string TipoProducto { get; set; } = "N/A";
    }

    // Models/OperatorInfo.cs
    public class OperatorInfo
    {
        public string EpcOperador { get; set; }
        public string NombreOperador { get; set; }
        public string Area { get; set; }
        public DateTime? FechaAlta { get; set; }
    }
}
