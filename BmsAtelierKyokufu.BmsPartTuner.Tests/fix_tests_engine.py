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
            
            # 1. Replace __audioCache with something valid (or remove it if we just mock it)
            # Actually, if the test is mocking audioCache, it might be named cache or _cache or it's not defined.
            # Let's replace __audioCache with udioCache if that's what the test intended, or just define it.
            # Many tests do: new SimulationEngine(files, __audioCache, __replaceTable, start, end) -> Wait! SimulationEngine only takes 4 args now!
            # It takes (fileList, audioCache, startPoint, endPoint)
            
            # Let's remove the __replaceTable from SimulationEngine constructor in tests if it's there
            rep(path, r'new SimulationEngine\(([^,]+),\s*__audioCache,\s*__replaceTable,\s*([^,]+),\s*([^)]+)\)', r'new SimulationEngine(\1, audioCache, \2, \3)')
            rep(path, r'new SimulationEngine\(([^,]+),\s*([^,]+),\s*__audioCache,\s*([^,]+),\s*([^)]+)\)', r'new SimulationEngine(\1, audioCache, \3, \4)')
            
            # ParallelAudioComparisonEngine takes 5 args: fileList, audioCache, replaceTable, start, end
            rep(path, r'new ParallelAudioComparisonEngine\(([^,]+),\s*__replaceTable,\s*([^,]+),\s*([^)]+)\)', r'new ParallelAudioComparisonEngine(\1, audioCache, __replaceTable, \2, \3)')
            rep(path, r'new ParallelAudioComparisonEngine\(([^,]+),\s*__audioCache,\s*__replaceTable,\s*([^,]+),\s*([^)]+)\)', r'new ParallelAudioComparisonEngine(\1, audioCache, __replaceTable, \2, \3)')
            
            # DefinitionReuse takes 2 args: fileList, audioCache
            rep(path, r'new DefinitionReuse\(([^)]+)\)', r'new DefinitionReuse(\1, audioCache)')
            # If it already had audioCache, it would become audioCache, audioCache. Let's fix that.
            rep(path, r'audioCache,\s*audioCache\)', r'audioCache)')
            
            # Rename __audioCache -> audioCache
            rep(path, r'__audioCache', r'audioCache')
            
            # Replace file.CachedData -> audioCache[file.Name]
            rep(path, r'(\w+)\.CachedData\.Dispose\(\);', r'audioCache[\1.Name].Dispose();')
            rep(path, r'(\w+)\.CachedData\.SampleRate', r'audioCache[\1.Name].SampleRate')
            rep(path, r'(\w+)\.CachedData', r'audioCache[\1.Name]')
            
            # file.ClearCache() -> remove
            rep(path, r'\w+\.ClearCache\(\);', r'')

print("Done")
