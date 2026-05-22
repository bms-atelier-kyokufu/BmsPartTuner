import os
import re

def rep(file, pattern, repl, flags=0):
    with open(file, 'r', encoding='utf-8') as f:
        c = f.read()
    c2 = re.sub(pattern, repl, c, flags=flags)
    if c != c2:
        with open(file, 'w', encoding='utf-8') as f:
            f.write(c2)
            print(f'Fixed {os.path.basename(file)}')

# 1. BmsDefinitionManager.BmsAudioFile -> BmsAudioFile in all tests
test_dir = r'p:\Projects\Active\BmsPartTuner\BmsAtelierKyokufu.BmsPartTuner.Tests'
for root, dirs, files in os.walk(test_dir):
    for file in files:
        if file.endswith('.cs'):
            path = os.path.join(root, file)
            rep(path, r'BmsDefinitionManager\.BmsAudioFile', 'BmsAudioFile')

# 2. Add using to DefinitionRangeManagerTests.cs
p1 = os.path.join(test_dir, r'Core\Bms\DefinitionRangeManagerTests.cs')
with open(p1, 'r', encoding='utf-8') as f:
    c = f.read()
if 'using BmsAtelierKyokufu.BmsPartTuner.Models;' not in c:
    c = 'using BmsAtelierKyokufu.BmsPartTuner.Models;\n' + c
    with open(p1, 'w', encoding='utf-8') as f:
        f.write(c)

# 3. AudioCacheManagerTests.cs
p2 = os.path.join(test_dir, r'Audio\AudioCacheManagerTests.cs')
rep(p2, r'__audioCache', 'cache')
rep(p2, r'wavFile\.CachedData\.Dispose\(\);', r'cache[wavFile.Name].Dispose();')
rep(p2, r'wavFile\.CachedData\.SampleRate', r'cache[wavFile.Name].SampleRate')
rep(p2, r'wavFile\.ClearCache\(\);', r'')

# 4. OptimizationViewModelTests.cs - Update interfaces
p3 = os.path.join(test_dir, r'ViewModels\OptimizationViewModelTests.cs')
interface_old = r'public Task<BmsOptimizationService\.ReductionResult> ExecuteDefinitionReductionAsync\(\s*IReadOnlyList<BmsAudioFile> fileList,\s*string inputPath,\s*string outputPath,\s*float r2Threshold,\s*int startDefinition,\s*int endDefinition,\s*IProgress<int>\? progress = null,\s*IEnumerable<string>\? selectedKeywords = null\)'
interface_new = r'public Task<BmsOptimizationService.ReductionResult> ExecuteDefinitionReductionAsync(\n            IReadOnlyList<BmsAudioFile> fileList,\n            string inputPath,\n            string outputPath,\n            float r2Threshold,\n            int startDefinition,\n            int endDefinition,\n            bool isPhysicalDeletionEnabled,\n            IProgress<int>? progress = null,\n            IEnumerable<string>? selectedKeywords = null)'
rep(p3, interface_old, interface_new)

print("Done")
