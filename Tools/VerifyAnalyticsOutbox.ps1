# Uses the same Unity compiler, Mono runtime and Newtonsoft library as the cached project.
param([string]$EditorData, [switch]$NetworkSmokeTest)
$ErrorActionPreference = 'Stop'
$project = Split-Path $PSScriptRoot -Parent
$report = Get-ChildItem -Path (Join-Path $project 'Library/Bee/artifacts/*/HomeTasks.rsp') |
    Sort-Object LastWriteTime -Descending | Select-Object -First 1
if (-not $report) { throw 'Open the project in Unity once to populate its compiler response file.' }
$lines = Get-Content -LiteralPath $report.FullName
if (-not $EditorData) {
    $editorRef = $lines | Where-Object { $_ -match '^-r:"(.+)/Managed/UnityEngine/UnityEngine.dll"$' } | Select-Object -First 1
    if ($editorRef -notmatch '^-r:"(.+)/Managed/UnityEngine/UnityEngine.dll"$') { throw 'Specify -EditorData pointing to the Unity Editor/Data folder.' }
    $EditorData = $Matches[1]
}
$json = Get-ChildItem -Path (Join-Path $project 'Library/PackageCache/com.unity.nuget.newtonsoft-json@*/Runtime/Newtonsoft.Json.dll') |
    Select-Object -First 1
if (-not $json) { throw 'The project Newtonsoft package must be restored by Unity.' }
$tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
$work = [IO.Path]::GetFullPath((Join-Path $tempRoot ('focus-shift-verify-' + [guid]::NewGuid().ToString('N'))))
[IO.Directory]::CreateDirectory($work) | Out-Null
$apiProcess = $null
Push-Location $project
try {
    $dotnet = Join-Path $EditorData 'NetCoreRuntime/dotnet.exe'
    $compiler = Join-Path $EditorData 'DotNetSdkRoslyn/csc.dll'
    $ref = Join-Path $EditorData 'MonoBleedingEdge/lib/mono/4.8-api'
    $runtimeRsp = $lines | Where-Object { $_ -notmatch '^-out:|^-refout:' }
    $sources = @('Core/AnalyticsOutbox','Core/GameServerOrigin','Core/SessionAnalytics','Core/SessionSubmissionRequest','UI/StudyStartPanel')
    foreach ($name in $sources) {
        $source = "Assets/HomeTasks/Scripts/$name.cs"
        if ($runtimeRsp -notcontains ('"' + $source + '"') -and $runtimeRsp -notcontains $source) { $runtimeRsp += $source }
    }
    $runtimeRsp += '-out:"' + $work + '/HomeTasks.dll"'
    $rsp = Join-Path $work 'compile.rsp'
    [IO.File]::WriteAllLines($rsp, [string[]]$runtimeRsp)
    & $dotnet $compiler ("@" + $rsp)
    if ($LASTEXITCODE -ne 0) { throw 'HomeTasks compilation failed.' }
    Write-Output 'PASS HomeTasks compiles against Unity project references'
    $editorReport = Join-Path $report.DirectoryName 'Assembly-CSharp-Editor.rsp'
    if (Test-Path -LiteralPath $editorReport) {
        $editorRsp = (Get-Content -LiteralPath $editorReport) | Where-Object { $_ -notmatch '^-out:|^-refout:' }
        $editorRsp = $editorRsp -replace '^-r:".*/HomeTasks\.ref\.dll"$', ('-r:"' + $work + '/HomeTasks.dll"')
        $source = 'Assets/HomeTasks/Editor/StudyServerSetup.cs'
        if ($editorRsp -notcontains ('"' + $source + '"') -and $editorRsp -notcontains $source) { $editorRsp += $source }
        $editorRsp += '-out:"' + $work + '/Assembly-CSharp-Editor.dll"'
        $editorCompile = Join-Path $work 'compile-editor.rsp'
        [IO.File]::WriteAllLines($editorCompile, [string[]]$editorRsp)
        & $dotnet $compiler ("@" + $editorCompile)
        if ($LASTEXITCODE -ne 0) { throw 'Study server build menu compilation failed.' }
        Write-Output 'PASS study server build menu compiles'
    }
    $program = Join-Path $work 'verify.exe'
    & $dotnet $compiler /nologo /nostdlib /langversion:9 "/out:$program" "/r:$ref/mscorlib.dll" "/r:$ref/System.dll" "/r:$ref/System.Core.dll" "/r:$ref/Facades/netstandard.dll" ("/r:" + $json.FullName) Assets/HomeTasks/Scripts/Core/AnalyticsOutbox.cs Tools/VerifyAnalyticsOutbox.cs
    if ($LASTEXITCODE -ne 0) { throw 'Outbox verifier compilation failed.' }
    Copy-Item -LiteralPath $json.FullName -Destination $work
    & (Join-Path $EditorData 'MonoBleedingEdge/bin/mono.exe') $program
    if ($LASTEXITCODE -ne 0) { throw 'Outbox regression failed.' }
    if ($NetworkSmokeTest) {
        $smoke = Join-Path $work 'OfflineNetwork'
        foreach ($sub in 'Assets','Packages','ProjectSettings') {
            [IO.Directory]::CreateDirectory((Join-Path $smoke $sub)) | Out-Null
        }
        $assets = Join-Path $smoke 'Assets'
        Copy-Item -LiteralPath $json.FullName -Destination $assets
        foreach ($name in 'AnalyticsOutbox','SessionSubmissionRequest') {
            Copy-Item -LiteralPath "Assets/HomeTasks/Scripts/Core/$name.cs" -Destination $assets
        }
        Copy-Item -LiteralPath 'Tools/VerifyAnalyticsTransport.cs' -Destination $assets
        Copy-Item -LiteralPath 'ProjectSettings/ProjectVersion.txt' -Destination (Join-Path $smoke 'ProjectSettings')
        [IO.File]::WriteAllText((Join-Path $smoke 'Packages/manifest.json'), '{"dependencies":{"com.unity.modules.unitywebrequest":"1.0.0"}}')
        if (-not (Test-Path -LiteralPath (Join-Path $project 'Analytics/node_modules/express'))) { throw 'Run npm ci in Analytics first.' }
        $rsa = [Security.Cryptography.RSA]::Create(2048)
        try {
            $certRequest = [Security.Cryptography.X509Certificates.CertificateRequest]::new('CN=127.0.0.1', $rsa, [Security.Cryptography.HashAlgorithmName]::SHA256, [Security.Cryptography.RSASignaturePadding]::Pkcs1)
            $san = [Security.Cryptography.X509Certificates.SubjectAlternativeNameBuilder]::new()
            $san.AddIpAddress([ipaddress]::Loopback)
            $certRequest.CertificateExtensions.Add($san.Build())
            $certificate = $certRequest.CreateSelfSigned([datetimeoffset]::UtcNow.AddMinutes(-1), [datetimeoffset]::UtcNow.AddHours(1))
            try {
                [IO.File]::WriteAllBytes((Join-Path $smoke 'test-server.der'), $certificate.RawData)
                [IO.File]::WriteAllBytes((Join-Path $smoke 'test-server.pfx'), $certificate.Export([Security.Cryptography.X509Certificates.X509ContentType]::Pfx, 'test-only'))
            } finally { $certificate.Dispose() }
        } finally { $rsa.Dispose() }
        $apiLog = Join-Path $work 'api.stdout'
        $apiError = Join-Path $work 'api.stderr'
        $apiProcess = Start-Process -FilePath (Get-Command node.exe).Source -ArgumentList @('Tools/VerifyAnalyticsServer.mjs',('"'+$smoke+'"')) -WorkingDirectory $project -WindowStyle Hidden -PassThru -RedirectStandardOutput $apiLog -RedirectStandardError $apiError
        $ready = Join-Path $smoke 'api.prepared'
        $deadline = [datetime]::UtcNow.AddSeconds(20)
        while (-not (Test-Path -LiteralPath $ready)) {
            if ($apiProcess.HasExited -or [datetime]::UtcNow -gt $deadline) {
                Get-Content -LiteralPath $apiError -Tail 15
                throw 'Local Express and SQLite fixture could not start.'
            }
            Start-Sleep -Milliseconds 100
        }
        $trace = Join-Path $work 'network.log'
        $editor = Join-Path (Split-Path $EditorData -Parent) 'Unity.exe'
        $arguments = @('-batchmode','-nographics','-disable-assembly-updater','-projectPath',('"'+$smoke+'"'),'-logFile',('"'+$trace+'"'),'-executeMethod','VerifyAnalyticsTransport.Run')
        $player = Start-Process -FilePath $editor -ArgumentList $arguments -WindowStyle Hidden -PassThru
        $timedOut = -not $player.WaitForExit(180000)
        if ($timedOut) { $player.Kill(); $player.WaitForExit() }
        if ($timedOut -or $player.ExitCode -ne 0 -or -not (Select-String -LiteralPath $trace -Pattern 'FOCUS_SHIFT_TRANSPORT_PASS' -Quiet)) {
            if (Test-Path -LiteralPath $trace) {
                Get-Content -LiteralPath $trace | Select-String -Pattern 'error CS\d+|Exception:|failed to|time.?out|aborting batchmode' |
                    Select-Object -Last 12 | ForEach-Object { $_.Line }
            }
            throw 'Isolated Unity transport smoke test failed.'
        }
        Write-Output 'PASS native UnityWebRequest requests automatic player codes and offline retries preserve the queued packet'
        if (-not $apiProcess.WaitForExit(10000) -or $apiProcess.ExitCode -ne 0 -or -not (Select-String -LiteralPath $apiLog -Pattern 'FOCUS_SHIFT_SQLITE_PASS' -Quiet)) {
            Get-Content -LiteralPath $apiError -Tail 20
            throw 'Native submissions did not produce the expected SQLite results.'
        }
        Write-Output 'PASS native HTTPS stores separate rounds for P001 and P002 and does not duplicate the replayed round'
    }
}
finally {
    if ($apiProcess -and -not $apiProcess.HasExited) { $apiProcess.Kill(); $apiProcess.WaitForExit() }
    if ($apiProcess) { $apiProcess.Dispose() }
    if ($player) { $player.Dispose() }
    Pop-Location
    if ((Split-Path -Parent $work).TrimEnd('\') -eq $tempRoot.TrimEnd('\') -and
        (Split-Path -Leaf $work) -match '^focus-shift-verify-[a-f0-9]{32}$') {
        # Unity package/shader helpers can briefly retain the directory after Editor exit.
        for ($attempt = 0; $attempt -lt 40 -and (Test-Path -LiteralPath $work); $attempt++) {
            try { Remove-Item -LiteralPath $work -Recurse -Force -ErrorAction Stop }
            catch [IO.IOException] { if ($attempt -eq 39) { throw }; Start-Sleep -Milliseconds 500 }
        }
    }
}
