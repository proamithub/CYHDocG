# ==============================================================================
# CYHDocGuru Fast Incremental Live Deployment Script
# Only uploads modified / new files based on MD5 content hashes
# Target: patnaorthoclinic.com (Host: 208.91.198.227)
# ==============================================================================

param(
    [switch]$WebOnly,
    [switch]$ApiOnly,
    [switch]$Force
)

$ftpHost = "208.91.198.227"
$ftpUser = "patnag5f"
$ftpPass = "CYHBrand@PassAK"

$webLocalPath = Join-Path $PSScriptRoot "publish\web"
$apiLocalPath = Join-Path $PSScriptRoot "publish\api"

$webRemoteBase = "ftp://$ftpHost/cyhdocg.patnaorthoclinic.com"
$apiRemoteBase = "ftp://$ftpHost/cyhdocgapi.patnaorthoclinic.com"

$manifestPath = Join-Path $PSScriptRoot ".deploy_manifest.json"

# Load previous deployment manifest
$global:manifest = @{
    web = @{}
    api = @{}
}

if (Test-Path $manifestPath) {
    try {
        $json = Get-Content -Path $manifestPath -Raw | ConvertFrom-Json
        if ($json.web) {
            foreach ($prop in $json.web.PSObject.Properties) {
                $global:manifest.web[$prop.Name] = $prop.Value
            }
        }
        if ($json.api) {
            foreach ($prop in $json.api.PSObject.Properties) {
                $global:manifest.api[$prop.Name] = $prop.Value
            }
        }
    } catch {
        Write-Host "Notice: Initializing fresh deployment manifest." -ForegroundColor Yellow
    }
}

function Save-Manifest {
    try {
        $global:manifest | ConvertTo-Json -Depth 5 | Set-Content -Path $manifestPath -Encoding UTF8
    } catch {
        Write-Host "Warning: Could not save deploy manifest: $($_.Exception.Message)" -ForegroundColor Yellow
    }
}

$global:createdDirs = @{}

function Ensure-FtpDirectory($ftpDirUri) {
    if ($global:createdDirs.ContainsKey($ftpDirUri)) {
        return
    }
    try {
        $req = [System.Net.FtpWebRequest]::Create($ftpDirUri)
        $req.Method = [System.Net.WebRequestMethods+Ftp]::MakeDirectory
        $req.Credentials = New-Object System.Net.NetworkCredential($ftpUser, $ftpPass)
        $req.KeepAlive = $false
        $req.Timeout = 10000
        $res = $req.GetResponse()
        $res.Close()
    } catch {
        # Directory might already exist
    }
    $global:createdDirs[$ftpDirUri] = $true
}

function Enable-AppOffline($remoteUri) {
    try {
        $offlineUri = "$remoteUri/app_offline.htm"
        $req = [System.Net.FtpWebRequest]::Create($offlineUri)
        $req.Method = [System.Net.WebRequestMethods+Ftp]::UploadFile
        $req.Credentials = New-Object System.Net.NetworkCredential($ftpUser, $ftpPass)
        $req.UseBinary = $true
        $req.KeepAlive = $false
        $req.Timeout = 10000
        $html = "<html><body><h1>Site Under Maintenance - Deploying Fast Update...</h1></body></html>"
        $bytes = [System.Text.Encoding]::UTF8.GetBytes($html)
        $req.ContentLength = $bytes.Length
        $stream = $req.GetRequestStream()
        $stream.Write($bytes, 0, $bytes.Length)
        $stream.Close()
        $res = $req.GetResponse()
        $res.Close()
        Write-Host ">>> App offline mode activated: $remoteUri" -ForegroundColor Yellow
        Start-Sleep -Seconds 1
    } catch {
        Write-Host "Notice: Offline mode trigger skipped: $($_.Exception.Message)" -ForegroundColor Gray
    }
}

function Disable-AppOffline($remoteUri) {
    try {
        $offlineUri = "$remoteUri/app_offline.htm"
        $req = [System.Net.FtpWebRequest]::Create($offlineUri)
        $req.Method = [System.Net.WebRequestMethods+Ftp]::DeleteFile
        $req.Credentials = New-Object System.Net.NetworkCredential($ftpUser, $ftpPass)
        $req.KeepAlive = $false
        $req.Timeout = 10000
        $res = $req.GetResponse()
        $res.Close()
        Write-Host ">>> App offline mode cleared. Site is LIVE: $remoteUri" -ForegroundColor Green
    } catch {
        # Ignore if file not found
    }
}

function Upload-FtpFolder($localDir, $remoteUri, $targetKey) {
    Write-Host "`n>>> Scanning files in: $localDir" -ForegroundColor Cyan
    Write-Host ">>> Target Destination: $remoteUri" -ForegroundColor Cyan

    $allFiles = Get-ChildItem -Path $localDir -Recurse -File
    $totalCount = $allFiles.Count
    $targetManifest = $global:manifest[$targetKey]

    # Find modified / new files based on MD5 hash
    $filesToUpload = @()
    foreach ($file in $allFiles) {
        $relative = $file.FullName.Substring($localDir.Length).TrimStart("\", "/")
        
        # Skip unnecessary .exe files if locked on shared hosting
        if ($file.Extension -eq ".exe") {
            continue
        }

        $hash = (Get-FileHash -Path $file.FullName -Algorithm MD5).Hash
        $storedHash = $targetManifest[$relative]

        if ($Force -or (-not $storedHash) -or ($storedHash -ne $hash)) {
            $filesToUpload += @{
                File = $file
                Relative = $relative
                Hash = $hash
            }
        }
    }

    if ($filesToUpload.Count -eq 0) {
        Write-Host "[OK] All $totalCount files are already up-to-date on live server. No upload needed!" -ForegroundColor Green
        return
    }

    Write-Host ">>> Incremental Sync: Uploading $($filesToUpload.Count) changed/new file(s) out of $totalCount total files..." -ForegroundColor Yellow

    # If DLLs or critical files are changing, toggle app_offline
    $hasBinaries = $filesToUpload | Where-Object { $_.File.Extension -in @(".dll", ".config", ".json") }
    if ($hasBinaries) {
        Enable-AppOffline $remoteUri
    }

    $current = 0
    $success = 0
    $failed = 0

    foreach ($item in $filesToUpload) {
        $current++
        $relative = $item.Relative
        $remoteFileUri = "$remoteUri/" + ($relative -replace "\\", "/")
        
        # Ensure parent remote directories exist
        $parts = $relative.Split("\/")
        if ($parts.Length -gt 1) {
            $currPath = $remoteUri
            for ($i = 0; $i -lt ($parts.Length - 1); $i++) {
                $currPath += "/" + $parts[$i]
                Ensure-FtpDirectory $currPath
            }
        }

        try {
            $req = [System.Net.FtpWebRequest]::Create($remoteFileUri)
            $req.Method = [System.Net.WebRequestMethods+Ftp]::UploadFile
            $req.Credentials = New-Object System.Net.NetworkCredential($ftpUser, $ftpPass)
            $req.UseBinary = $true
            $req.KeepAlive = $false
            $req.Timeout = 25000

            $fileBytes = [System.IO.File]::ReadAllBytes($item.File.FullName)
            $req.ContentLength = $fileBytes.Length

            $stream = $req.GetRequestStream()
            $stream.Write($fileBytes, 0, $fileBytes.Length)
            $stream.Close()

            $res = $req.GetResponse()
            $res.Close()

            $success++
            # Update manifest entry
            $targetManifest[$relative] = $item.Hash
            Write-Host "[$current/$($filesToUpload.Count)] Uploaded: $relative" -ForegroundColor Green
        } catch {
            $failed++
            Write-Host "[$current/$($filesToUpload.Count)] Failed: $relative - $($_.Exception.Message)" -ForegroundColor Red
        }
    }

    if ($hasBinaries) {
        Disable-AppOffline $remoteUri
    }

    Save-Manifest

    Write-Host "`n>>> Destination Upload Summary: Uploaded=$success, Failed=$failed (Total untouched=$($totalCount - $filesToUpload.Count))`n" -ForegroundColor Yellow
}

Write-Host "==================================================" -ForegroundColor Magenta
Write-Host " CYHDocGuru Fast Incremental FTP Deployment Tool" -ForegroundColor Magenta
Write-Host " Host: $ftpHost | User: $ftpUser" -ForegroundColor Magenta
Write-Host " Mode: $(if ($Force) { 'FORCE ALL' } else { 'ONLY CHANGED FILES (Delta Sync)' })" -ForegroundColor Magenta
Write-Host "==================================================" -ForegroundColor Magenta

if (-not $ApiOnly) {
    if (Test-Path $webLocalPath) {
        Upload-FtpFolder $webLocalPath $webRemoteBase "web"
    } else {
        Write-Host "Web publish folder not found at: $webLocalPath" -ForegroundColor Red
    }
}

if (-not $WebOnly) {
    if (Test-Path $apiLocalPath) {
        Upload-FtpFolder $apiLocalPath $apiRemoteBase "api"
    } else {
        Write-Host "Api publish folder not found at: $apiLocalPath" -ForegroundColor Red
    }
}

Write-Host "==================================================" -ForegroundColor Magenta
Write-Host " All Live Deployment Tasks Completed!" -ForegroundColor Magenta
Write-Host " Web Portal: https://cyhdocg.patnaorthoclinic.com" -ForegroundColor Magenta
Write-Host " REST API:   https://cyhdocgapi.patnaorthoclinic.com" -ForegroundColor Magenta
Write-Host "==================================================" -ForegroundColor Magenta
