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

test_dir = r'p:\Projects\Active\BmsPartTuner\BmsAtelierKyokufu.BmsPartTuner.Tests'

for root, dirs, files in os.walk(test_dir):
    for file in files:
        if file.endswith('.cs'):
            path = os.path.join(root, file)
            
            # Remove any remaining CachedData = ...
            rep(path, r',\s*CachedData\s*=\s*[^,\}]+', '')
            rep(path, r'CachedData\s*=\s*[^,\}]+\s*,', '')
            rep(path, r'CachedData\s*=\s*[^,\}]+', '')
            
            # Fix AudioFileGroupingStrategyTests.cs GroupFiles calls
            # Usually groupingStrategy.GroupFiles(fileList, start, end)
            rep(path, r'groupingStrategy\.GroupFiles\(([^,]+),\s*([^,]+),\s*([^)]+)\)', r'groupingStrategy.GroupFiles(audioCache, \1, \2, \3)')
            rep(path, r'strategy\.GroupFiles\(([^,]+),\s*([^,]+),\s*([^)]+)\)', r'strategy.GroupFiles(audioCache, \1, \2, \3)')
            
            # Also audioCache was missing in some places
            rep(path, r'__audioCache', r'audioCache')

            # We need audioCache declared if it's missing in tests
            # Just do it manually where it might be missing
            # E.g. in BmsOptimizationServiceTests_ExceptionHandling.cs
            rep(path, r'var preloadResult = AudioCacheManager\.PreloadAudioData', r'var audioCache = new System.Collections.Concurrent.ConcurrentDictionary<string, CachedSoundData>();\n            var preloadResult = AudioCacheManager.PreloadAudioData')

            # BmsAudioFile has no CachedData property, some tests used reflection or directly assigned.
            rep(path, r'var prop = typeof\(BmsAudioFile\)\.GetProperty\("CachedData"\);', r'var prop = null;')
            
            # Fix missing namespace in RadixConvertTests.cs (it used CachedSoundData)
            if file == 'RadixConvertTests.cs':
                with open(path, 'r', encoding='utf-8') as f:
                    c = f.read()
                if 'using BmsAtelierKyokufu.BmsPartTuner.Models;' not in c:
                    with open(path, 'w', encoding='utf-8') as f:
                        f.write('using BmsAtelierKyokufu.BmsPartTuner.Models;\n' + c)
                        print(f'Fixed {file}')

print("Done")
