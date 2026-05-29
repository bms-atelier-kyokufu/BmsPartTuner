using System.Threading.Tasks;
using BmsAtelierKyokufu.BmsPartTuner.Services.Bms.Bmson;

namespace BmsAtelierKyokufu.BmsPartTuner.Services.Bms
{
    public class BmsonConversionService : IBmsonConversionService
    {
        public Task<string> GenerateBmsTextAsync(string bmsonPath, bool keyNotesOnly = false)
        {
            return Task.Run(() => BmsonIntegrationFacade.GenerateBmsText(bmsonPath, keyNotesOnly));
        }
    }
}
