import os
import re

def fix_tests(file_path):
    with open(file_path, 'r', encoding='utf-8') as f:
        c = f.read()

    # Add audioCache initialization after [Fact] or [Theory] or similar test setup
    def add_cache(match):
        return match.group(0) + '\n            var audioCache = new System.Collections.Concurrent.ConcurrentDictionary<string, CachedSoundData>();'

    c = re.sub(r'public void [a-zA-Z0-9_]+\([^)]*\)\s*\{', add_cache, c)

    # Rewrite CachedData inline assignment
    # e.g. var file1 = new BmsAudioFile { Name = "a.wav", NumInteger = 1, CachedData = CreateDistinctCache(440.0) };
    def replace_cached_data(match):
        var_name = match.group(1)
        name = match.group(2)
        num = match.group(3)
        cache_val = match.group(4)
        
        lines = []
        lines.append(f'var {var_name} = new BmsAudioFile {{ Name = "{name}", NumInteger = {num} }};')
        if cache_val.strip() != 'null':
            lines.append(f'audioCache["{name}"] = {cache_val};')
        return '\n            '.join(lines)
    
    # Matches: var file1 = new BmsAudioFile { Name = "a.wav", NumInteger = 1, CachedData = whatever };
    c = re.sub(r'var (\w+) = new BmsAudioFile \{ Name = "([^"]+)", NumInteger = ([^,]+),\s*CachedData = ([^}]+)\s*\};', replace_cached_data, c)

    # Also handle multiline object initialization
    # Name = "...",
    # NumInteger = ...,
    # CachedData = ...
    def replace_multiline(match):
        var_name = match.group(1)
        name = match.group(2)
        num = match.group(3)
        cache_val = match.group(4).strip()
        
        lines = []
        lines.append(f'var {var_name} = new BmsAudioFile\n            {{\n                Name = "{name}",\n                NumInteger = {num}\n            }};')
        if cache_val and cache_val != 'null' and cache_val != 'null // CachedData=nullでもカウントは行われる':
            clean_cache = cache_val.split('//')[0].strip()
            if clean_cache.endswith(','): clean_cache = clean_cache[:-1]
            if clean_cache and clean_cache != 'null':
                lines.append(f'audioCache["{name}"] = {clean_cache};')
        return '\n            '.join(lines)
        
    c = re.sub(r'var (\w+) = new BmsAudioFile\s*\{\s*Name = "([^"]+)",\s*NumInteger = ([^,]+),\s*CachedData = ([^\}]+?)\s*\};', replace_multiline, c)

    with open(file_path, 'w', encoding='utf-8') as f:
        f.write(c)

test_dir = r'p:\Projects\Active\BmsPartTuner\BmsAtelierKyokufu.BmsPartTuner.Tests'
for root, dirs, files in os.walk(test_dir):
    for file in files:
        if file.endswith('.cs'):
            fix_tests(os.path.join(root, file))

print("Done")
