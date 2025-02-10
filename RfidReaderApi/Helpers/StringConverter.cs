using System;
using System.Text.Json;
using System.Text.Json.Serialization;


namespace RfidReaderApi.Helpers
{
    public class StringConverter : JsonConverter<string>
    {
        public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            // Convertir números o cadenas a string
            if (reader.TokenType == JsonTokenType.String)
            {
                return reader.GetString();
            }
            else if (reader.TokenType == JsonTokenType.Number)
            {
                return reader.GetDouble().ToString(); // Convertir a cadena
            }

            throw new JsonException("Valor no válido para una cadena.");
        }

        public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value);
        }
    }
}
