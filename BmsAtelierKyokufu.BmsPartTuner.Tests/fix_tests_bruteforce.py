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
            
            with open(path, 'r', encoding='utf-8') as f:
                c = f.read()

            modified = False

            # 1. Add using if missing
            if 'using BmsAtelierKyokufu.BmsPartTuner.Models;' not in c and ('BmsAudioFile' in c or 'CachedSoundData' in c or 'ReductionResult' in c):
                c = 'using BmsAtelierKyokufu.BmsPartTuner.Models;\n' + c
                modified = True

            # 2. Fix audioCache not found or declared before use
            # Remove any existing declarations first to avoid duplicates
            c = re.sub(r'\s*var audioCache = new System\.Collections\.Concurrent\.ConcurrentDictionary<string, CachedSoundData>\(\);', '', c)
            
            # Now insert it at the start of every [Fact] or [Theory] method body
            def add_cache(match):
                return match.group(0) + '\n            var audioCache = new System.Collections.Concurrent.ConcurrentDictionary<string, CachedSoundData>();'
            c = re.sub(r'(?:\[Fact\]|\[Theory\])[\s\S]*?public (?:async Task|void) [a-zA-Z0-9_]+\([^)]*\)\s*\{', add_cache, c)
            modified = True

            # 3. Strip all CachedData = ... comprehensively
            # This will match CachedData = CreateMock(), CachedData = null, etc. ending with comma or brace
            c = re.sub(r'CachedData\s*=\s*[^,}]*(?:,|(?=\}))', '', c)
            
            # Also handle multiline trailing commas that might have been left
            c = re.sub(r',\s*\}', '\n            }', c)
            
            # Replace inline file.CachedData
            c = re.sub(r'([a-zA-Z0-9_]+)\.CachedData', r'audioCache[\1.Name]', c)

            if modified:
                with open(path, 'w', encoding='utf-8') as f:
                    f.write(c)
                print(f'Fixed {file}')

print("Done")
