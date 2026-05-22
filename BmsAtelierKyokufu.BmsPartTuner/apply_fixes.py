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

# 1. BmsOptimizationService.cs
p1 = r'p:\Projects\Active\BmsPartTuner\BmsAtelierKyokufu.BmsPartTuner\Services\BmsOptimizationService.cs'
# Lines 481, 483, 484: Remove file.CachedData access
rep(p1, r'if \(file\.CachedData == null\)\s*\{\s*continue;\s*\}', r'if (!audioCache.ContainsKey(file.Name))\n                {\n                    continue;\n                }')
rep(p1, r'float rms = file\.CachedData\.TotalRms;', r'float rms = audioCache[file.Name].TotalRms;')
rep(p1, r'file\.ClearCache\(\);', r'')
# Lines 553: _audioCache -> audioCache
rep(p1, r'_audioCache\[file\.Name\]', r'audioCache[file.Name]')
# Lines 558, 560, 561: Remove file.CachedData access
rep(p1, r'if \(file\.CachedData == null \|\| target\.CachedData == null\)', r'if (!audioCache.ContainsKey(file.Name) || !audioCache.ContainsKey(target.Name))')
rep(p1, r'float similarity = CompareAudio\(file\.CachedData, target\.CachedData\);', r'float similarity = CompareAudio(audioCache[file.Name], audioCache[target.Name]);')

# 2. SimulationEngine.cs
p2 = r'p:\Projects\Active\BmsPartTuner\BmsAtelierKyokufu.BmsPartTuner\Core\Optimization\SimulationEngine.cs'
rep(p2, r'public SimulationEngine\(\s*IReadOnlyList<BmsAudioFile> fileList,\s*int startPoint,\s*int endPoint\)', r'public SimulationEngine(\n        IReadOnlyList<BmsAudioFile> fileList,\n        IReadOnlyDictionary<string, CachedSoundData> audioCache,\n        int startPoint,\n        int endPoint)')

# 3. ParallelAudioComparisonEngine.cs
p3 = r'p:\Projects\Active\BmsPartTuner\BmsAtelierKyokufu.BmsPartTuner\Audio\ParallelAudioComparisonEngine.cs'
rep(p3, r'public ParallelAudioComparisonEngine\(\s*IReadOnlyList<BmsAudioFile> fileList,\s*int\[\] replaceTable,\s*int startPoint,\s*int endPoint\)', r'public ParallelAudioComparisonEngine(\n        IReadOnlyList<BmsAudioFile> fileList,\n        IReadOnlyDictionary<string, CachedSoundData> audioCache,\n        int[] replaceTable,\n        int startPoint,\n        int endPoint)')

# 4. AudioCacheManager.cs
p4 = r'p:\Projects\Active\BmsPartTuner\BmsAtelierKyokufu.BmsPartTuner\Audio\AudioCacheManager.cs'
rep(p4, r'LogCacheStatistics\(cachedCount, successCount, failCount, loaded, totalFiles, totalMemoryMB, null\)', r'LogCacheStatistics(cachedCount, successCount, failCount, loaded, totalFiles, totalMemoryMB)')
rep(p4, r'return \(failedFiles\.ToList\(\), cache\);', r'return (failedFiles.ToList(), audioCache);')

# 5. AudioFileGroupingStrategy.cs
# We'll use a manual replacement script for this to ensure accuracy since we had trouble earlier.
p5 = r'p:\Projects\Active\BmsPartTuner\BmsAtelierKyokufu.BmsPartTuner\Core\Helpers\AudioFileGroupingStrategy.cs'
rep(p5, r'public List<List<int>> GroupFiles\(\s*IReadOnlyList<BmsAudioFile> files,\s*int startPoint,\s*int endPoint\)', r'public List<List<int>> GroupFiles(\n        IReadOnlyDictionary<string, CachedSoundData> audioCache,\n        IReadOnlyList<BmsAudioFile> files,\n        int startPoint,\n        int endPoint)')
rep(p5, r'private List<List<int>> GroupFilesByKeywords\(\s*IReadOnlyList<BmsAudioFile> files,\s*int startPoint,\s*int endPoint,\s*IEnumerable<string> keywords\)', r'private List<List<int>> GroupFilesByKeywords(\n        IReadOnlyDictionary<string, CachedSoundData> audioCache,\n        IReadOnlyList<BmsAudioFile> files,\n        int startPoint,\n        int endPoint,\n        IEnumerable<string> keywords)')
rep(p5, r'private List<List<int>> GroupFilesTraditional\(\s*IReadOnlyList<BmsAudioFile> files,\s*int startPoint,\s*int endPoint\)', r'private List<List<int>> GroupFilesTraditional(\n        IReadOnlyDictionary<string, CachedSoundData> audioCache,\n        IReadOnlyList<BmsAudioFile> files,\n        int startPoint,\n        int endPoint)')
rep(p5, r'return GroupFilesByKeywords\(files, startPoint, endPoint, keywords\);', r'return GroupFilesByKeywords(audioCache, files, startPoint, endPoint, keywords);')
rep(p5, r'return GroupFilesTraditional\(files, startPoint, endPoint\);', r'return GroupFilesTraditional(audioCache, files, startPoint, endPoint);')

# 6. OptimizationViewModel.cs
p6 = r'p:\Projects\Active\BmsPartTuner\BmsAtelierKyokufu.BmsPartTuner\ViewModels\OptimizationViewModel.cs'
rep(p6, r'if \(_bmsFiles != null\)\s*\{\s*AudioCacheManager\.CleanupAudioCache\(_bmsFiles\);\s*\}', r'if (bmsFileList != null)\n                {\n                    var files = bmsFileList.GetFileList();\n                    if (files != null) BmsAtelierKyokufu.BmsPartTuner.Audio.AudioCacheManager.CleanupAudioCache(files);\n                }')

print("Done")
