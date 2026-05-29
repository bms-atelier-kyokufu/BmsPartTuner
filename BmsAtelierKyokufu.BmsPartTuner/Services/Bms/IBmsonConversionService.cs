using System.Threading.Tasks;

namespace BmsAtelierKyokufu.BmsPartTuner.Services.Bms
{
    public interface IBmsonConversionService
    {
        Task<string> GenerateBmsTextAsync(string bmsonPath, bool keyNotesOnly = false);
    }
}
