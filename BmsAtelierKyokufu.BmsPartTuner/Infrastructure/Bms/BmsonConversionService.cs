using System.Threading.Tasks;
using BmsAtelierKyokufu.BmsPartTuner.Infrastructure.Bms.Bmson;
using BmsAtelierKyokufu.BmsPartTuner.Core.Interfaces.Bms;

namespace BmsAtelierKyokufu.BmsPartTuner.Infrastructure.Bms
{
    public class BmsonConversionService : IBmsonConversionService
    {
        public Task<string> GenerateBmsTextAsync(string bmsonPath, bool keyNotesOnly = false)
        {
            return Task.Run(() => BmsonIntegrationFacade.GenerateBmsText(bmsonPath, keyNotesOnly));
        }
    }
}
