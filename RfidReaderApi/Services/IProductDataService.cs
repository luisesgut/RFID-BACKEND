using RfidReaderApi.Models;

namespace RfidReaderApi.Services
{
    public interface IProductDataService
    {
        Task<ProductInfo> GetProductDataAsync(string epc);
        Task<OperatorInfo> GetOperatorInfoAsync(string epcOperador);
        Task UpdateStatusAsync(string epc, int newStatus);
        Task RegisterExtraInfoAsync(string epc);
        Task RegisterAntennaRecordAsync(string epc, string epcOperador);
    }
}
