using System.Text.Json.Serialization;
using RfidReaderApi.Helpers;

namespace RfidReaderApi.Models
{
    public class ProductResponse
    {
        public string NombreProducto { get; set; }
        public string UrlImagen { get; set; }
        public string Uom { get; set; }
        public string ProductPrintCard { get; set; }
        public string TipoEtiqueta { get; set; }
        public string Area { get; set; }
        public string ClaveProducto { get; set; }

        [JsonConverter(typeof(StringConverter))]
        public string Piezas { get; set; }

        [JsonConverter(typeof(StringConverter))]
        public string PesoNeto { get; set; }

        [JsonConverter(typeof(StringConverter))]
        public string PesoBruto { get; set; }

        [JsonConverter(typeof(StringConverter))]
        public string PesoTarima { get; set; }


        public string Rfid { get; set; }

        [JsonPropertyName("tipoProducto")]
        public string? TipoProducto { get; set; }
    }
}
