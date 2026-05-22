import os
import re

def rep(file, pattern, repl):
    with open(file, 'r', encoding='utf-8') as f:
        c = f.read()
    c2 = re.sub(pattern, repl, c)
    if c != c2:
        with open(file, 'w', encoding='utf-8') as f:
            f.write(c2)
            print(f'Fixed {os.path.basename(file)}')

# 1. ParallelAudioComparisonEngine.cs
p1 = r'p:\Projects\Active\BmsPartTuner\BmsAtelierKyokufu.BmsPartTuner\Audio\ParallelAudioComparisonEngine.cs'
rep(p1, r'__audioCache', r'_audioCache')
# Add field if missing
rep(p1, r'private readonly int _endPoint;', r'private readonly int _endPoint;\n    private readonly IReadOnlyDictionary<string, CachedSoundData> _audioCache;')

# 2. SimulationEngine.cs
p2 = r'p:\Projects\Active\BmsPartTuner\BmsAtelierKyokufu.BmsPartTuner\Core\Optimization\SimulationEngine.cs'
rep(p2, r'__audioCache', r'_audioCache')
rep(p2, r'private readonly int _endPoint;', r'private readonly int _endPoint;\n    private readonly IReadOnlyDictionary<string, CachedSoundData> _audioCache;')

# 3. AudioFileGroupingStrategy.cs
p3 = r'p:\Projects\Active\BmsPartTuner\BmsAtelierKyokufu.BmsPartTuner\Core\Helpers\AudioFileGroupingStrategy.cs'
# Signature updates
rep(p3, r'public List<List<int>> GroupFiles\(', r'public List<List<int>> GroupFiles(IReadOnlyDictionary<string, CachedSoundData> audioCache, ')
rep(p3, r'private List<List<int>> GroupFilesByKeywords\(', r'private List<List<int>> GroupFilesByKeywords(IReadOnlyDictionary<string, CachedSoundData> audioCache, ')
rep(p3, r'private List<List<int>> GroupFilesTraditional\(', r'private List<List<int>> GroupFilesTraditional(IReadOnlyDictionary<string, CachedSoundData> audioCache, ')
rep(p3, r'return GroupFilesByKeywords\(files, ', r'return GroupFilesByKeywords(audioCache, files, ')
rep(p3, r'return GroupFilesTraditional\(files, ', r'return GroupFilesTraditional(audioCache, files, ')

# 4. AudioCacheManager.cs
p4 = r'p:\Projects\Active\BmsPartTuner\BmsAtelierKyokufu.BmsPartTuner\Audio\AudioCacheManager.cs'
rep(p4, r'LogCacheStatistics\(cachedCount, successCount, failCount, loaded, totalFiles, totalMemoryMB\)', r'LogCacheStatistics(cachedCount, successCount, failCount, loaded, totalFiles, totalMemoryMB, null)')
rep(p4, r'return \(failedFiles\.ToList\(\), audioCache\);', r'return (failedFiles.ToList(), cache);')

# 5. BmsDefinitionManager.cs
p5 = r'p:\Projects\Active\BmsPartTuner\BmsAtelierKyokufu.BmsPartTuner\Models\BmsDefinitionManager.cs'
rep(p5, r'IEnumerable<FileList\.WavFiles>', r'IEnumerable<BmsAudioFile>')

# 6. BmsOptimizationService.cs
p6 = r'p:\Projects\Active\BmsPartTuner\BmsAtelierKyokufu.BmsPartTuner\Services\BmsOptimizationService.cs'
rep(p6, r'var simulationEngine = new SimulationEngine\(bmsFiles, startPoint, endPoint\);', r'var simulationEngine = new SimulationEngine(bmsFiles, audioCache, startPoint, endPoint);')
rep(p6, r'public async Task<BmsDefinitionManager> ExecuteDefinitionReductionAsync\([^)]+\)', r'public async Task<BmsDefinitionManager> ExecuteDefinitionReductionAsync(BmsDefinitionManager fileList, IProgress<int> progress, float r2Val, int defStart, int defEnd, bool isPhysicalDeletionEnabled)')
rep(p6, r'audioCache\s*=', r'_audioCache =')
rep(p6, r'_audioCache\[file\.Name\]\s*=\s*cachedData;', r'audioCache[file.Name] = cachedData;')

print("Done")
