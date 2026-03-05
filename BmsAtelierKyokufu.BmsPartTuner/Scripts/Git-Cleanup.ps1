# ==============================================================================
# 設計思想: 
# 1. 失敗の早期発見 (Fail-Fast): Gitリポジトリ外での実行を即座に中断する
# 2. 破壊的操作の明示: 強制削除の前に警告色を使用し、ユーザーに現状を伝える
# 3. 疎結合: 実行ポリシーの変更をスクリプト内に持たせず、利用者に委ねる
# ==============================================================================

# 1. Gitリポジトリ内かチェック
if (!(git rev-parse --is-inside-work-tree -as-quiet)) {
    Write-Host "エラー: ここはGitリポジトリではありません。" -ForegroundColor Red
    exit
}

Write-Host "リモートブランチの状態を同期中..." -ForegroundColor Cyan
git fetch --prune

# 2. 削除対象の抽出
# - [gone] とマークされたブランチを取得
# - 現在のブランチを除外
$currentBranch = git rev-parse --abbrev-ref HEAD
$goneBranches = git for-each-ref --format='%(refname:short) %(upstream:track)' refs/heads | 
    Where-Object { $_ -match '\[gone\]$' } | 
    ForEach-Object { ($_ -split ' ')[0] } | 
    Where-Object { $_ -ne $currentBranch }

# 3. 削除実行
if ($goneBranches) {
    Write-Host "以下のリモート消失済みブランチを削除します (Force Delete):" -ForegroundColor Yellow
    $goneBranches | ForEach-Object {
        Write-Host "  - $_" -ForegroundColor Gray
        git branch -D $_
    }
    Write-Host "クリーンアップが完了しました。" -ForegroundColor Green
} else {
    Write-Host "掃除が必要なブランチはありません（現在のブランチ: $currentBranch）。" -ForegroundColor Green
}
