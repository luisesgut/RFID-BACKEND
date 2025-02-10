// RfidReaderApi/Exceptions/ProductExceptions.cs
namespace RfidReaderApi.Exceptions
{
    public enum ProductErrorType
    {
        NotFound,
        AlreadyRegistered,
        InvalidData,
        NetworkError,
        Unknown
    }

    public class ProductDataException : Exception
    {
        public string EPC { get; }
        public ProductErrorType ErrorType { get; }

        public ProductDataException(string message, string epc, ProductErrorType errorType, Exception innerException = null)
            : base(message, innerException)
        {
            EPC = epc;
            ErrorType = errorType;
        }
    }
}