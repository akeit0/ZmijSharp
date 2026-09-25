$ErrorActionPreference = 'Stop'
$coreProject = Join-Path $PSScriptRoot 'ShortestCoreSize/ShortestCoreSize.csproj'
$checkProject = Join-Path $PSScriptRoot 'ShortestCoreSize.Check/ShortestCoreSize.Check.csproj'
$dll = Join-Path $PSScriptRoot 'ShortestCoreSize/bin/Release/net11.0/Shortest.Core.dll'

$results = @{}
foreach ($profile in @('Zmij', 'Unrounded')) {
    & dotnet build -c Release $coreProject -t:Rebuild "-p:ShortestCore=$profile" -v:q
    if ($LASTEXITCODE -ne 0) { throw "Build failed for $profile." }

    $bytes = (Get-Item -LiteralPath $dll).Length
    $dllHash = (Get-FileHash -LiteralPath $dll -Algorithm SHA256).Hash
    $output = & dotnet run -c Release --project $checkProject "-p:ShortestCore=$profile"
    if ($LASTEXITCODE -ne 0) { throw "Output check failed for $profile." }

    $result = $output | Select-Object -Last 1
    if ($result -notmatch '^\d+ [0-9A-F]{64}$') { throw "Unexpected check output for $profile`: $result" }
    $results[$profile] = @{ Bytes = $bytes; DllHash = $dllHash; Digest = $result }
    Write-Output "$profile`: $bytes bytes; $result"
}

if ($results.Zmij.Digest -ne $results.Unrounded.Digest) {
    throw 'The two shortest-producer profiles produced different digits or scales.'
}
if ($results.Zmij.DllHash -eq $results.Unrounded.DllHash) {
    throw 'The two profiles unexpectedly produced the same DLL.'
}
