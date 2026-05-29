using BmsAtelierKyokufu.BmsPartTuner.Models;
using BmsAtelierKyokufu.BmsPartTuner.Core.Optimization;
using BmsAtelierKyokufu.BmsPartTuner.Core.Interfaces.Bms;
using BmsAtelierKyokufu.BmsPartTuner.Infrastructure.Bms;

namespace BmsAtelierKyokufu.BmsPartTuner.Tests.Helpers
{
    public class ReductionTestOptions
    {
        public Action<BmsFileBuilder>? BuildBms { get; set; }
        public Func<string, List<BmsAudioFile>>? CreateFiles { get; set; }
        public Action<BmsOptimizationService.ReductionResult>? AssertResult { get; set; }
        public Action<string>? BeforeExecute { get; set; }
        public Action<string>? AfterExecute { get; set; }
        public float? Threshold { get; set; }
        public int StartDef { get; set; } = 1;
        public int EndDef { get; set; } = 1;
        public bool PhysicalDeletion { get; set; }
        public IEnumerable<string>? Keywords { get; set; }
    }
}
