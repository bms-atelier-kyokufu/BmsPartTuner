import os
import re

def replace_in_file(path):
    with open(path, 'r', encoding='utf-8') as f:
        content = f.read()

    orig = content
    
    # 1. Replace FileList.WavFiles -> BmsAudioFile
    content = re.sub(r'\bFileList\.WavFiles\b', 'BmsAudioFile', content)
    
    # 2. Replace WavFiles -> BmsAudioFile (where FileList was already omitted)
    content = re.sub(r'\bWavFiles\b', 'BmsAudioFile', content)
    
    # 3. Replace FileList -> BmsDefinitionManager
    content = re.sub(r'\bFileList\b', 'BmsDefinitionManager', content)

    if content != orig:
        with open(path, 'w', encoding='utf-8') as f:
            f.write(content)
        print(f"Updated {path}")

# Run on Main Project
src_dir = r'p:\Projects\Active\BmsPartTuner\BmsAtelierKyokufu.BmsPartTuner'
for root, dirs, files in os.walk(src_dir):
    for file in files:
        if file.endswith('.cs') and file != 'FileList.cs':
            replace_in_file(os.path.join(root, file))

# Run on Tests Project
test_dir = r'p:\Projects\Active\BmsPartTuner\BmsAtelierKyokufu.BmsPartTuner.Tests'
for root, dirs, files in os.walk(test_dir):
    for file in files:
        if file.endswith('.cs'):
            replace_in_file(os.path.join(root, file))

# Finally, delete FileList.cs
file_list_path = r'p:\Projects\Active\BmsPartTuner\BmsAtelierKyokufu.BmsPartTuner\Models\FileList.cs'
if os.path.exists(file_list_path):
    os.remove(file_list_path)
    print("Deleted FileList.cs")

print("Done")
