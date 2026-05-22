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

# 1. DefinitionReuse.cs
p1 = r'p:\Projects\Active\BmsPartTuner\BmsAtelierKyokufu.BmsPartTuner\Core\Bms\DefinitionReuse.cs'
rep(p1, r'private readonly List<BmsAudioFile> _fileList;', r'private readonly List<BmsAudioFile> _fileList;\n    private readonly IReadOnlyDictionary<string, CachedSoundData> _audioCache;')
rep(p1, r'public DefinitionReuse\(ObservableCollection<BmsAudioFile> fileList\)', r'public DefinitionReuse(ObservableCollection<BmsAudioFile> fileList, IReadOnlyDictionary<string, CachedSoundData> audioCache)')
rep(p1, r'_fileList = fileList\?\.ToList\(\) \?\? throw new ArgumentNullException\(nameof\(fileList\)\);', r'_fileList = fileList?.ToList() ?? throw new ArgumentNullException(nameof(fileList));\n        _audioCache = audioCache ?? throw new ArgumentNullException(nameof(audioCache));')
rep(p1, r'groupingStrategy\.GroupFiles\(_fileList,\s*_rangeManager\.StartPoint,\s*_rangeManager\.EndPoint,\s*selectedKeywords\);', r'groupingStrategy.GroupFiles(_audioCache, _fileList, _rangeManager.StartPoint, _rangeManager.EndPoint, selectedKeywords);')
rep(p1, r'new ParallelAudioComparisonEngine\(_fileList,\s*_replaces,\s*_rangeManager\.StartPoint,\s*_rangeManager\.EndPoint\);', r'new ParallelAudioComparisonEngine(_fileList, _audioCache, _replaces, _rangeManager.StartPoint, _rangeManager.EndPoint);')

# 2. SimulationEngine.cs
p2 = r'p:\Projects\Active\BmsPartTuner\BmsAtelierKyokufu.BmsPartTuner\Core\Optimization\SimulationEngine.cs'
rep(p2, r'groupingStrategy\.GroupFiles\(_fileList,\s*_startPoint,\s*_endPoint\);', r'groupingStrategy.GroupFiles(_audioCache, _fileList, _startPoint, _endPoint, null);')

# 3. BmsOptimizationService.cs
p3 = r'p:\Projects\Active\BmsPartTuner\BmsAtelierKyokufu.BmsPartTuner\Services\BmsOptimizationService.cs'
rep(p3, r'var simulationEngine = new SimulationEngine\(fileList,\s*startPoint,\s*endPoint\);', r'var simulationEngine = new SimulationEngine(fileList, audioCache, startPoint, endPoint);')
rep(p3, r'var definitionReuse = new DefinitionReuse\(observableList\);', r'var definitionReuse = new DefinitionReuse(observableList, audioCache);')

print("Done")
