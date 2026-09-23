param(
  [Parameter(Mandatory)] [string] $Base,
  [Parameter(Mandatory)] [ValidateSet('colle', 'scanne')] [string] $Chemin,
  [Parameter(Mandatory)] [string] $Materiel,
  [int] $Runs = 3,
  [string] $OllamaContainer = 'microservice_rgpd_ollama',
  [string] $Connexion = 'Server=localhost;Port=3307;User ID=root;Password=mesure;Database=dolibarr'
)

Add-Type -AssemblyName System.Net.Http
$ErrorActionPreference = 'Stop'
$ici = $PSScriptRoot
$collage = [IO.File]::ReadAllText((Join-Path $ici 'collage-dolibarr.jsonl'))

function New-Client {
  $handler = [System.Net.Http.HttpClientHandler]::new()
  $handler.AllowAutoRedirect = $false
  $handler.CookieContainer = [System.Net.CookieContainer]::new()
  $client = [System.Net.Http.HttpClient]::new($handler)
  $client.BaseAddress = [Uri]$Base
  $client.Timeout = [TimeSpan]::FromMinutes(10)
  $client
}

function Get-Token($client, $address) {
  $html = $client.GetStringAsync($address).GetAwaiter().GetResult()
  $m = [regex]::Match($html, '<input name="__RequestVerificationToken"[^>]*value="([^"]+)"')
  if (-not $m.Success) { throw "Aucun jeton sur $address" }
  $m.Groups[1].Value
}

function Post-Form($client, $address, [hashtable] $fields) {
  $pairs = [System.Collections.Generic.List[System.Collections.Generic.KeyValuePair[string, string]]]::new()
  foreach ($k in $fields.Keys) { $pairs.Add([System.Collections.Generic.KeyValuePair[string, string]]::new($k, $fields[$k])) }
  # FormUrlEncodedContent et Uri.EscapeDataString plafonnent la taille d'une valeur sous .NET Framework.
  $body = ($pairs | ForEach-Object { [Net.WebUtility]::UrlEncode($_.Key) + '=' + [Net.WebUtility]::UrlEncode($_.Value) }) -join '&'
  $content = [System.Net.Http.StringContent]::new($body, [Text.Encoding]::UTF8, 'application/x-www-form-urlencoded')
  $client.PostAsync($address, $content).GetAwaiter().GetResult()
}

# Les lignes GIN d'Ollama : « [GIN] 2026/09/14 - 12:30:01 | 200 | 1.234s | 172.17.0.1 | POST "/api/embed" ».
function Get-EmbedCalls([datetime] $sinceUtc) {
  $since = $sinceUtc.ToString('yyyy-MM-ddTHH:mm:ssZ')
  $lines = cmd /c "docker logs --since $since $OllamaContainer 2>&1"
  $calls = foreach ($l in $lines) {
    # Le « µ » s'écrit par son code : Windows PowerShell 5.1 lit un script sans BOM en ANSI.
    $m = [regex]::Match($l, '\|\s*(\d{3})\s*\|\s*([\d.]+)(\xB5s|ms|s|m)\S*\s*\|.*POST\s+"/api/embed"')
    if ($m.Success) {
      $v = [double]::Parse($m.Groups[2].Value, [Globalization.CultureInfo]::InvariantCulture)
      $ms = switch ($m.Groups[3].Value) { "$([char]0xB5)s" { $v / 1000 } 'ms' { $v } 's' { $v * 1000 } 'm' { $v * 60000 } }
      [pscustomobject]@{ Status = [int]$m.Groups[1].Value; Ms = $ms }
    }
  }
  @($calls)
}

$resultats = @()
for ($run = 1; $run -le $Runs; $run++) {
  $client = New-Client
  $debut = [datetime]::UtcNow
  $mesure = [ordered]@{ chemin = $Chemin; materiel = $Materiel; essai = $run }

  if ($Chemin -eq 'colle') {
    $token = Get-Token $client '/detection/depot'
    $sw = [Diagnostics.Stopwatch]::StartNew()
    $rep = Post-Form $client '/detection/depot' @{ '__RequestVerificationToken' = $token; 'Paste' = $collage }
    $sw.Stop()
    $mesure.statut = [int]$rep.StatusCode
    $mesure.geste_ms = $sw.Elapsed.TotalMilliseconds
    if ($rep.StatusCode -ne 302) { $mesure.corps = $rep.Content.ReadAsStringAsync().GetAwaiter().GetResult().Substring(0, 500) }
  }
  else {
    $token = Get-Token $client '/detection/connexion'
    $sw = [Diagnostics.Stopwatch]::StartNew()
    $rep = Post-Form $client '/detection/connexion' @{ '__RequestVerificationToken' = $token; 'Dialect' = 'mariadb'; 'ConnectionString' = $Connexion }
    if ($rep.StatusCode -ne 302) { throw "Lancement refusé : $([int]$rep.StatusCode) $($rep.Content.ReadAsStringAsync().GetAwaiter().GetResult())" }
    $adresse = $rep.Headers.Location.OriginalString
    $phases = [ordered]@{}
    $fin = $null
    while ($true) {
      $r = $client.GetAsync($adresse).GetAwaiter().GetResult()
      $t = $sw.Elapsed.TotalMilliseconds
      if ($r.StatusCode -ne 200) { $fin = [int]$r.StatusCode; break }
      $html = [Net.WebUtility]::HtmlDecode($r.Content.ReadAsStringAsync().GetAwaiter().GetResult())
      $p = [regex]::Match($html, 'Phase : <strong>([^<]+)</strong>')
      if ($p.Success -and -not $phases.Contains($p.Groups[1].Value)) { $phases[$p.Groups[1].Value] = $t }
      if ($html -notmatch 'http-equiv="refresh"') { $fin = 'fin-sans-rapport'; break }
      Start-Sleep -Milliseconds 50
    }
    $sw.Stop()
    $mesure.statut = $fin
    $mesure.geste_ms = $sw.Elapsed.TotalMilliseconds
    $mesure.phases_debut_ms = $phases
  }

  Start-Sleep -Seconds 1
  $calls = Get-EmbedCalls $debut
  $mesure.lots_embed = $calls.Count
  $mesure.embed_somme_ms = ($calls | Measure-Object Ms -Sum).Sum
  $mesure.embed_max_ms = ($calls | Measure-Object Ms -Maximum).Maximum
  $mesure.embed_statuts = (($calls | Group-Object Status | ForEach-Object { "$($_.Name)x$($_.Count)" }) -join ',')
  $resultats += [pscustomobject]$mesure
  ([pscustomobject]$mesure | ConvertTo-Json -Compress -Depth 4)
  $client.Dispose()
}

$sortie = Join-Path $ici "mesures-$Materiel-$Chemin.json"
$resultats | ConvertTo-Json -Depth 4 | Set-Content -Encoding utf8 $sortie
