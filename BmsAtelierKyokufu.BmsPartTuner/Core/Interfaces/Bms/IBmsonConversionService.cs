namespace BmsAtelierKyokufu.BmsPartTuner.Core.Interfaces.Bms
{
    public interface IBmsonConversionService
    {
        Task<string> GenerateBmsTextAsync(string bmsonPath, bool keyNotesOnly = false, IProgress<int>? progress = null, CancellationToken cancellationToken = default);
    }
}
