using BmsAtelierKyokufu.BmsPartTuner.Infrastructure.Bms.Bmson;
namespace BmsAtelierKyokufu.BmsPartTuner.Infrastructure.Bms
{
    public class BmsonConversionService : IBmsonConversionService
    {
        public Task<string> GenerateBmsTextAsync(string bmsonPath, bool keyNotesOnly = false, IProgress<int>? progress = null)
        {
            return Task.Run(() => BmsonIntegrationFacade.GenerateBmsText(bmsonPath, keyNotesOnly, progress));
        }
    }
}
