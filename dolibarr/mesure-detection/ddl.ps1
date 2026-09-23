$bt = [char]96
$sb = [Text.StringBuilder]::new()
[void]$sb.AppendLine('SET FOREIGN_KEY_CHECKS=0;')
# Le schéma Dolibarr du corpus, relu en DDL : A2 ne lit que les noms, index et clés sont hors sujet.
$pivot = Join-Path $PSScriptRoot '..\..\corpus\schemas\pivots\dolibarr.jsonl'
$rows = Get-Content $pivot -Encoding utf8 |
  ForEach-Object { $_ | ConvertFrom-Json } | Where-Object { $_.table }
foreach ($g in ($rows | Group-Object table)) {
  $cols = $g.Group | Sort-Object { [int]$_.position } | ForEach-Object {
    $null_ = if ($_.nullable -eq 0) { ' NOT NULL' } else { '' }
    '  ' + $bt + $_.colonne + $bt + ' ' + $_.type + $null_
  }
  [void]$sb.AppendLine('CREATE TABLE ' + $bt + $g.Name + $bt + ' (' + [Environment]::NewLine + ($cols -join (',' + [Environment]::NewLine)) + [Environment]::NewLine + ') ENGINE=InnoDB ROW_FORMAT=DYNAMIC DEFAULT CHARSET=utf8mb4;')
}
$p = Join-Path $PSScriptRoot 'dolibarr.sql'
[IO.File]::WriteAllText($p, $sb.ToString())
"tables: $(($rows | Group-Object table).Count)"
