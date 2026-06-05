[CmdletBinding()]
param (
    [Parameter(Position = 0)]
    [string]$TargetDir = $null,

    [Parameter(Position = 1)]
    [string]$GlobalUsingsPath = $null,

    [Parameter(Position = 2)]
    [int]$PromotionThreshold = 3
)

# ==============================================================================
# Helper class definitions for C# global using and FQN refactoring
# ==============================================================================

# Manages Git safety backups before executing destructive refactoring operations
class GitBackupManager {
    [string]$TargetDir

    GitBackupManager([string]$targetDir) {
        $this.TargetDir = $targetDir
    }

    # Commits all dirty working tree changes to a temporary safe commit
    [void]CreateSafetyCommit() {
        Write-Host "Checking Git status..."
        $gitStatus = git -C $this.TargetDir status --porcelain 2>$null
        if ($LASTEXITCODE -eq 0 -and $gitStatus) {
            Write-Host "Uncommitted changes detected. Creating temporary safety commit..."
            git -C $this.TargetDir add -A
            $timestamp = Get-Date -Format 'yyyyMMdd_HHmmss'
            git -C $this.TargetDir commit -m "wip: auto-saved before global using update $timestamp"
            Write-Host "Git state saved."
        } elseif ($LASTEXITCODE -ne 0) {
            Write-Host "Warning: Git command failed or directory is not a git repository. Skipping auto-save."
        } else {
            Write-Host "Git directory is clean. No safety commit needed."
        }
    }
}

# Scans target directory and indexes namespaces and custom class/struct/enum definitions
class CsharpProject {
    [string]$TargetDir
    [string]$GlobalUsingsPath
    $CsFiles            # ArrayList of FileInfo objects
    $TypeToNs           # Dictionary[string, string] (case-sensitive)
    $NsToTypes          # Dictionary[string, HashSet[string]]
    $KnownNamespaces    # HashSet[string] (case-insensitive)

    CsharpProject([string]$targetDir, [string]$globalUsingsPath) {
        $this.TargetDir = $targetDir
        $this.GlobalUsingsPath = $globalUsingsPath
        
        $this.CsFiles = New-Object System.Collections.ArrayList
        $this.TypeToNs = New-Object 'System.Collections.Generic.Dictionary[string, string]' -ArgumentList @([System.StringComparer]::Ordinal)
        $this.NsToTypes = New-Object 'System.Collections.Generic.Dictionary[string, System.Collections.Generic.HashSet[string]]' -ArgumentList @([System.StringComparer]::Ordinal)
        $this.KnownNamespaces = New-Object 'System.Collections.Generic.HashSet[string]' -ArgumentList @([System.StringComparer]::OrdinalIgnoreCase)
    }

    # Performs recursive scanning on target directory
    [void]Scan() {
        $rawFiles = Get-ChildItem -Path $this.TargetDir -Filter *.cs -Recurse
        if ($null -eq $rawFiles) {
            return
        }

        # Populate CsFiles, ignoring build outputs, auto-generated code, and the scripts directory
        foreach ($file in $rawFiles) {
            if ($null -eq $file -or $file.PSIsContainer -or [string]::IsNullOrEmpty($file.FullName)) {
                continue
            }
            if ($file.FullName -match '\\(bin|obj|Generated|Scripts)\\') {
                continue
            }
            if ($file.FullName -eq $this.GlobalUsingsPath) {
                continue
            }
            [void]$this.CsFiles.Add($file)
        }

        # Parse namespaces and defined class/struct/interface names
        foreach ($file in $this.CsFiles) {
            $fileBytes = [System.IO.File]::ReadAllBytes($file.FullName)
            $fileHasBom = $fileBytes.Length -ge 3 -and $fileBytes[0] -eq 0xEF -and $fileBytes[1] -eq 0xBB -and $fileBytes[2] -eq 0xBF
            $fileEncoding = if ($fileHasBom) { New-Object System.Text.UTF8Encoding($true) } else { New-Object System.Text.UTF8Encoding($false) }

            $content = [System.IO.File]::ReadAllText($file.FullName, $fileEncoding)
            
            # Identify namespace
            $fileNamespace = $null
            if ($content -match '(?m)^\s*namespace\s+([A-Za-z0-9_\.]+);?') {
                $fileNamespace = $Matches[1].Trim().TrimEnd(';')
                [void]$this.KnownNamespaces.Add($fileNamespace)
            }

            if ([string]::IsNullOrEmpty($fileNamespace)) {
                continue
            }

            # Map all types defined in the namespace
            $typeMatches = [regex]::Matches($content, '\b(?:class|struct|interface|enum|record)\s+([A-Za-z0-9_]+)\b')
            foreach ($match in $typeMatches) {
                $typeName = $match.Groups[1].Value
                if (-not $this.TypeToNs.ContainsKey($typeName)) {
                    $this.TypeToNs[$typeName] = $fileNamespace
                }
                if (-not $this.NsToTypes.ContainsKey($fileNamespace)) {
                    $this.NsToTypes[$fileNamespace] = New-Object 'System.Collections.Generic.HashSet[string]' -ArgumentList @([System.StringComparer]::Ordinal)
                }
                [void]$this.NsToTypes[$fileNamespace].Add($typeName)
            }
        }
    }
}

# Reduces fully qualified class names to short names while adding required using statements
class FqnReducer {
    $Project    # CsharpProject reference

    FqnReducer($project) {
        $this.Project = $project
    }

    # Reducse FQNs inside registered project files
    [System.Collections.ArrayList]ReduceFqns() {
        $modifiedFiles = New-Object System.Collections.ArrayList
        
        # Match only FQNs belonging to our own project prefix
        $fqnPattern = '(?<!namespace\s+)(?<!using\s+)(?<!global\s+using\s+)\b(BmsAtelierKyokufu\.BmsPartTuner\.(?:[A-Za-z0-9_]+\.)+)([A-Za-z0-9_]+)\b'
        $fqnRegex = New-Object regex($fqnPattern)

        foreach ($file in $this.Project.CsFiles) {
            $fileBytes = [System.IO.File]::ReadAllBytes($file.FullName)
            $fileHasBom = $fileBytes.Length -ge 3 -and $fileBytes[0] -eq 0xEF -and $fileBytes[1] -eq 0xBB -and $fileBytes[2] -eq 0xBF
            $fileEncoding = if ($fileHasBom) { New-Object System.Text.UTF8Encoding($true) } else { New-Object System.Text.UTF8Encoding($false) }

            $content = [System.IO.File]::ReadAllText($file.FullName, $fileEncoding)
            $originalContent = $content

            $usingsToAdd = New-Object 'System.Collections.Generic.HashSet[string]' -ArgumentList @([System.StringComparer]::Ordinal)
            $hasReplacement = $false

            $matches = $fqnRegex.Matches($content)
            
            # Replace backwards to prevent character offset disruption
            for ($i = $matches.Count - 1; $i -ge 0; $i--) {
                $m = $matches[$i]
                $fullNs = $m.Groups[1].Value.TrimEnd('.')
                $typeName = $m.Groups[2].Value

                # Resolve context line details to ensure we are not inside a using alias directive
                $lineStart = 0
                if ($m.Index -gt 0) {
                    $lastNewline = $content.LastIndexOf("`n", $m.Index - 1)
                    if ($lastNewline -ge 0) {
                        $lineStart = $lastNewline + 1
                    }
                }
                $lineEnd = $content.IndexOf("`n", $m.Index)
                if ($lineEnd -lt 0) { $lineEnd = $content.Length }
                $lineText = $content.Substring($lineStart, $lineEnd - $lineStart)

                # Skip replacements if FQN is on a using alias declaration line
                if ($lineText -match '^\s*(global\s+)?using\s+[A-Za-z0-9_]+\s*=') {
                    continue
                }

                # Apply substitution only if matching our detected target definitions
                if ($this.Project.KnownNamespaces.Contains($fullNs) -and $this.Project.NsToTypes.ContainsKey($fullNs) -and $this.Project.NsToTypes[$fullNs].Contains($typeName)) {
                    $content = $content.Substring(0, $m.Index) + $typeName + $content.Substring($m.Index + $m.Length)
                    [void]$usingsToAdd.Add($fullNs)
                    $hasReplacement = $true
                }
            }

            if ($hasReplacement) {
                $lines = New-Object System.Collections.ArrayList
                $rawLines = $content -split '\r?\n'
                [void]$lines.AddRange($rawLines)

                $existingUsings = New-Object 'System.Collections.Generic.HashSet[string]' -ArgumentList @([System.StringComparer]::Ordinal)
                $lastUsingIndex = -1
                $namespaceIndex = -1
                
                # Scan for existing using and namespace locations
                for ($j = 0; $j -lt $lines.Count; $j++) {
                    $line = $lines[$j]
                    if ($line -match '^\s*using\s+([^;]+);') {
                        [void]$existingUsings.Add($Matches[1].Trim())
                        $lastUsingIndex = $j
                    } elseif ($line -match '^\s*namespace\s+') {
                        $namespaceIndex = $j
                    }
                }

                # Insert missing using directives appropriately
                $insertedCount = 0
                foreach ($ns in $usingsToAdd) {
                    if (-not $existingUsings.Contains($ns)) {
                        $usingLine = "using $ns;"
                        if ($lastUsingIndex -ge 0) {
                            $lines.Insert($lastUsingIndex + 1 + $insertedCount, $usingLine)
                            $insertedCount++
                        } elseif ($namespaceIndex -ge 0) {
                            $lines.Insert($namespaceIndex + $insertedCount, $usingLine)
                            $insertedCount++
                        } else {
                            $lines.Insert($insertedCount, $usingLine)
                            $insertedCount++
                        }
                    }
                }

                $newline = if ($originalContent -contains "`r`n") { "`r`n" } elseif ($originalContent -contains "`n") { "`n" } else { [Environment]::NewLine }
                $newText = [string]::Join($newline, [string[]]$lines.ToArray())
                $newText = $newText -replace "^(\r?\n)+", ""

                [System.IO.File]::WriteAllText($file.FullName, $newText, $fileEncoding)
                Write-Host "Reduced FQN in: $($file.FullName)"
                [void]$modifiedFiles.Add($file)
            }
        }
        return $modifiedFiles
    }
}

# Performs global using promotion/demotion logic and cleans redundant using directives
class GlobalUsingsManager {
    $Project            # CsharpProject reference
    [int]$Threshold

    GlobalUsingsManager($project, [int]$threshold) {
        $this.Project = $project
        $this.Threshold = $threshold
    }

    # Core logic to audit using frequency, modify GlobalUsings.cs, and deduplicate
    [void]UpdateGlobalUsings([System.Collections.ArrayList]$modifiedFiles) {
        Write-Host "Analyzing using statements usage frequency for global promotion..."
        $usingCounts = New-Object 'System.Collections.Generic.Dictionary[string, int]' -ArgumentList @([System.StringComparer]::Ordinal)

        # Count references of internal usings
        foreach ($file in $this.Project.CsFiles) {
            $fileBytes = [System.IO.File]::ReadAllBytes($file.FullName)
            $fileHasBom = $fileBytes.Length -ge 3 -and $fileBytes[0] -eq 0xEF -and $fileBytes[1] -eq 0xBB -and $fileBytes[2] -eq 0xBF
            $fileEncoding = if ($fileHasBom) { New-Object System.Text.UTF8Encoding($true) } else { New-Object System.Text.UTF8Encoding($false) }

            $lines = [System.IO.File]::ReadAllLines($file.FullName, $fileEncoding)
            foreach ($line in $lines) {
                if ($line -match '^\s*using\s+([^;]+);') {
                    $ns = $Matches[1].Trim()
                    if ($this.Project.KnownNamespaces.Contains($ns)) {
                        if (-not $usingCounts.ContainsKey($ns)) {
                            $usingCounts[$ns] = 0
                        }
                        $usingCounts[$ns]++
                    }
                }
            }
        }

        # Analyze current contents of GlobalUsings.cs
        $globalUsings = New-Object 'System.Collections.Generic.HashSet[string]' -ArgumentList @([System.StringComparer]::Ordinal)
        $globalUsingsBytes = [System.IO.File]::ReadAllBytes($this.Project.GlobalUsingsPath)
        $globalUsingsHasBom = $globalUsingsBytes.Length -ge 3 -and $globalUsingsBytes[0] -eq 0xEF -and $globalUsingsBytes[1] -eq 0xBB -and $globalUsingsBytes[2] -eq 0xBF
        $globalUsingsEncoding = if ($globalUsingsHasBom) { New-Object System.Text.UTF8Encoding($true) } else { New-Object System.Text.UTF8Encoding($false) }

        $globalUsingsLines = New-Object System.Collections.ArrayList
        [void]$globalUsingsLines.AddRange([System.IO.File]::ReadAllLines($this.Project.GlobalUsingsPath, $globalUsingsEncoding))

        foreach ($line in $globalUsingsLines) {
            if ($line -match '^\s*global\s+using\s+([^;]+);') {
                $ns = $Matches[1].Trim()
                [void]$globalUsings.Add($ns)
            }
        }

        # Promote usings that surpass the threshold count
        $promotedAny = $false
        foreach ($ns in $usingCounts.Keys) {
            $count = $usingCounts[$ns]
            if ($count -ge $this.Threshold -and -not $globalUsings.Contains($ns)) {
                Write-Host "Promoting namespace to GlobalUsings: $ns (Used in $count files)"
                
                # Determine standard position to insert new global usings
                $insertIndex = -1
                for ($k = 0; $k -lt $globalUsingsLines.Count; $k++) {
                    $line = $globalUsingsLines[$k]
                    if ($line -match '^\s*using\s+' -or $line -match '^\s*\[assembly:') {
                        $insertIndex = $k
                        break
                    }
                }
                
                $globalUsingLine = "global using $ns;"
                if ($insertIndex -ge 0) {
                    $globalUsingsLines.Insert($insertIndex, $globalUsingLine)
                } else {
                    [void]$globalUsingsLines.Add($globalUsingLine)
                }
                [void]$globalUsings.Add($ns)
                $promotedAny = $true
            }
        }

        # Demote (remove) global usings that are no longer referenced in project
        Write-Host "Scanning for unused global usings..."
        $demotedAny = $false
        $usingsToDemote = New-Object 'System.Collections.Generic.HashSet[string]' -ArgumentList @([System.StringComparer]::Ordinal)

        foreach ($ns in $globalUsings) {
            if (-not $this.Project.KnownNamespaces.Contains($ns)) {
                continue
            }

            if ($this.Project.NsToTypes.ContainsKey($ns)) {
                $typesInNs = $this.Project.NsToTypes[$ns]
                $typeUsed = $false
                
                # Check for usage in any of the registered project CS files
                foreach ($file in $this.Project.CsFiles) {
                    $fileBytes = [System.IO.File]::ReadAllBytes($file.FullName)
                    $fileHasBom = $fileBytes.Length -ge 3 -and $fileBytes[0] -eq 0xEF -and $fileBytes[1] -eq 0xBB -and $fileBytes[2] -eq 0xBF
                    $fileEncoding = if ($fileHasBom) { New-Object System.Text.UTF8Encoding($true) } else { New-Object System.Text.UTF8Encoding($false) }
                    $fileText = [System.IO.File]::ReadAllText($file.FullName, $fileEncoding)

                    foreach ($t in $typesInNs) {
                        if ($fileText -match "\b$t\b") {
                            $typeUsed = $true
                            break
                        }
                    }
                    if ($typeUsed) { break }
                }

                if (-not $typeUsed) {
                    Write-Host "Demoting unused global using: $ns"
                    [void]$usingsToDemote.Add($ns)
                    $demotedAny = $true
                }
            }
        }

        # Cleanup demoted items from GlobalUsings.cs array
        if ($demotedAny) {
            for ($k = $globalUsingsLines.Count - 1; $k -ge 0; $k--) {
                $line = $globalUsingsLines[$k]
                if ($line -match '^\s*global\s+using\s+([^;]+);') {
                    $ns = $Matches[1].Trim()
                    if ($usingsToDemote.Contains($ns)) {
                        $globalUsingsLines.RemoveAt($k)
                        [void]$globalUsings.Remove($ns)
                    }
                }
            }
        }

        # Save GlobalUsings.cs back to file
        if ($promotedAny -or $demotedAny) {
            $globalUsingsRaw = [System.IO.File]::ReadAllText($this.Project.GlobalUsingsPath, $globalUsingsEncoding)
            $globalUsingsNewline = if ($globalUsingsRaw -contains "`r`n") { "`r`n" } elseif ($globalUsingsRaw -contains "`n") { "`n" } else { [Environment]::NewLine }
            
            $newGlobalUsingsText = [string]::Join($globalUsingsNewline, [string[]]$globalUsingsLines.ToArray())
            [System.IO.File]::WriteAllText($this.Project.GlobalUsingsPath, $newGlobalUsingsText, $globalUsingsEncoding)
            Write-Host "Updated GlobalUsings.cs successfully."
        }

        # Clean redundant normal using statements from C# files
        Write-Host "Removing redundant normal using statements..."
        $totalRemoved = 0
        $modifiedFilesCount = 0

        foreach ($file in $this.Project.CsFiles) {
            $fileBytes = [System.IO.File]::ReadAllBytes($file.FullName)
            $fileHasBom = $fileBytes.Length -ge 3 -and $fileBytes[0] -eq 0xEF -and $fileBytes[1] -eq 0xBB -and $fileBytes[2] -eq 0xBF
            $fileEncoding = if ($fileHasBom) { New-Object System.Text.UTF8Encoding($true) } else { New-Object System.Text.UTF8Encoding($false) }

            $lines = [System.IO.File]::ReadAllLines($file.FullName, $fileEncoding)
            $newLines = New-Object System.Collections.ArrayList
            $fileRemovedCount = 0

            foreach ($line in $lines) {
                if ($line -match '^\s*using\s+([^;]+);') {
                    $usingContent = $Matches[1].Trim()
                    if ($globalUsings.Contains($usingContent)) {
                        $fileRemovedCount++
                        $totalRemoved++
                        continue
                    }
                }
                [void]$newLines.Add($line)
            }

            if ($fileRemovedCount -gt 0 -or $modifiedFiles.Contains($file)) {
                $rawText = [System.IO.File]::ReadAllText($file.FullName, $fileEncoding)
                $newline = if ($rawText -contains "`r`n") { "`r`n" } elseif ($rawText -contains "`n") { "`n" } else { [Environment]::NewLine }

                $newText = [string]::Join($newline, [string[]]$newLines.ToArray())
                $newText = $newText -replace "^(\r?\n)+", ""

                [System.IO.File]::WriteAllText($file.FullName, $newText, $fileEncoding)
                Write-Host "Cleaned: $($file.FullName) (Removed $fileRemovedCount redundant using(s))"
                $modifiedFilesCount++
            }
        }

        Write-Host "Finished. Cleaned $modifiedFilesCount file(s), removed $totalRemoved global using duplicates in total."
    }
}

# ==============================================================================
# Script Execution Entrypoint
# ==============================================================================

try {
    $ErrorActionPreference = "Stop"

    # Dynamic target path resolution
    $scriptDir = $PSScriptRoot
    if ([string]::IsNullOrEmpty($scriptDir)) {
        $scriptDir = Split-Path $MyInvocation.MyCommand.Path -Parent
    }
    if ([string]::IsNullOrEmpty($TargetDir)) {
        $TargetDir = Split-Path $scriptDir -Parent
    }
    if ([string]::IsNullOrEmpty($GlobalUsingsPath)) {
        $GlobalUsingsPath = Join-Path $TargetDir "GlobalUsings.cs"
    }

    Write-Host "Target Directory: $TargetDir"
    Write-Host "Global Usings File: $GlobalUsingsPath"
    Write-Host "Promotion Threshold: $PromotionThreshold"

    # 1. Execute Git safety commit backup
    $gitManager = [GitBackupManager]::new($TargetDir)
    $gitManager.CreateSafetyCommit()

    # 2. Analyze C# project directories
    $project = [CsharpProject]::new($TargetDir, $GlobalUsingsPath)
    $project.Scan()

    # 3. Perform FQN to using reduction
    $reducer = [FqnReducer]::new($project)
    $modifiedFiles = $reducer.ReduceFqns()

    # 4. Promoted frequent usings and clean redundant usings
    $usingsManager = [GlobalUsingsManager]::new($project, $PromotionThreshold)
    $usingsManager.UpdateGlobalUsings($modifiedFiles)

    Write-Host "All tasks completed successfully."
} catch {
    Write-Host "An error occurred during execution."
    Write-Host "Error message: $_"
    Write-Host "Stack Trace:"
    Write-Host $_.ScriptStackTrace
    Write-Host "Exception details: $($_.Exception.ToString())"
    exit 1
}
