import os
import re

files_to_fix = [
    r'p:\Projects\Active\BmsPartTuner\BmsAtelierKyokufu.BmsPartTuner\Core\Helpers\AudioFileGroupingStrategy.cs',
    r'p:\Projects\Active\BmsPartTuner\BmsAtelierKyokufu.BmsPartTuner\Core\Bms\BmsFileRewriter.cs',
    r'p:\Projects\Active\BmsPartTuner\BmsAtelierKyokufu.BmsPartTuner\Core\Bms\DefinitionRangeManager.cs',
    r'p:\Projects\Active\BmsPartTuner\BmsAtelierKyokufu.BmsPartTuner\Core\Bms\DefinitionReuse.cs',
    r'p:\Projects\Active\BmsPartTuner\BmsAtelierKyokufu.BmsPartTuner\Core\Bms\DefinitionStatistics.cs',
    r'p:\Projects\Active\BmsPartTuner\BmsAtelierKyokufu.BmsPartTuner\Services\FileListFilterService.cs',
    r'p:\Projects\Active\BmsPartTuner\BmsAtelierKyokufu.BmsPartTuner\Services\InstrumentNameDetectionService.cs'
]

using_statement = 'using BmsAtelierKyokufu.BmsPartTuner.Models;\n'

for path in files_to_fix:
    with open(path, 'r', encoding='utf-8') as f:
        content = f.read()
    
    if using_statement not in content:
        # Add it right after the first using or at the top
        content = using_statement + content
        with open(path, 'w', encoding='utf-8') as f:
            f.write(content)
        print(f"Fixed {os.path.basename(path)}")

print("Done")
