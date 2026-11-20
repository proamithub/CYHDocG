# ==============================================================================
# CYHDocGuru 1-Click Live Deployment Script
# Target: patnaorthoclinic.com (Host: 208.91.198.227)
# ==============================================================================

param(
    [switch]$WebOnly,
    [switch]$ApiOnly
)

$ftpHost = "208.91.198.227"
$ftpUser = "patnag5f"
$ftpPass = "CYHBrand@PassAK"

$webLocalPath = Join-Path $PSScriptRoot "publish\web"
$apiLocalPath = Join-Path $PSScriptRoot "publish\api"

$webRemoteBase = "ftp://$ftpHost/cyhdocg.patnaorthoclinic.com"
$apiRemoteBase = "ftp://$ftpHost/cyhdocgapi.patnaorthoclinic.com"

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
        $html = "<html><body><h1>Site Under Maintenance - Deploying Update...</h1></body></html>"
        $bytes = [System.Text.Encoding]::UTF8.GetBytes($html)
        $req.ContentLength = $bytes.Length
        $stream = $req.GetRequestStream()
        $stream.Write($bytes, 0, $bytes.Length)
        $stream.Close()
        $res = $req.GetResponse()
        $res.Close()
        Write-Host ">>> App offline mode activated: $remoteUri" -ForegroundColor Yellow
        Start-Sleep -Seconds 2
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

function Upload-FtpFolder($localDir, $remoteUri) {
    Write-Host "`n>>> Starting Upload from: $localDir" -ForegroundColor Cyan
    Write-Host ">>> Target Destination: $remoteUri" -ForegroundColor Cyan

    Enable-AppOffline $remoteUri

    $allFiles = Get-ChildItem -Path $localDir -Recurse -File
    $total = $allFiles.Count
    $current = 0
    $success = 0
    $failed = 0

    foreach ($file in $allFiles) {
        $current++
        $relative = $file.FullName.Substring($localDir.Length).TrimStart("\", "/")
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

            $fileBytes = [System.IO.File]::ReadAllBytes($file.FullName)
            $req.ContentLength = $fileBytes.Length

            $stream = $req.GetRequestStream()
            $stream.Write($fileBytes, 0, $fileBytes.Length)
            $stream.Close()

            $res = $req.GetResponse()
            $res.Close()

            $success++
            Write-Host "[$current/$total] Uploaded: $relative" -ForegroundColor Green
        } catch {
            $failed++
            Write-Host "[$current/$total] Failed: $relative - $($_.Exception.Message)" -ForegroundColor Red
        }
    }

    Disable-AppOffline $remoteUri

    Write-Host "`n>>> Destination Upload Summary: Success=$success, Failed=$failed for $remoteUri`n" -ForegroundColor Yellow
}

Write-Host "==================================================" -ForegroundColor Magenta
Write-Host " CYHDocGuru Automated FTP Deployment Tool" -ForegroundColor Magenta
Write-Host " Host: $ftpHost | User: $ftpUser" -ForegroundColor Magenta
Write-Host "==================================================" -ForegroundColor Magenta

if (-not $ApiOnly) {
    if (Test-Path $webLocalPath) {
        Upload-FtpFolder $webLocalPath $webRemoteBase
    } else {
        Write-Host "Web publish folder not found at: $webLocalPath" -ForegroundColor Red
    }
}

if (-not $WebOnly) {
    if (Test-Path $apiLocalPath) {
        Upload-FtpFolder $apiLocalPath $apiRemoteBase
    } else {
        Write-Host "Api publish folder not found at: $apiLocalPath" -ForegroundColor Red
    }
}

Write-Host "==================================================" -ForegroundColor Magenta
Write-Host " All Live Deployment Tasks Completed!" -ForegroundColor Magenta
Write-Host " Web Portal: https://cyhdocg.patnaorthoclinic.com" -ForegroundColor Magenta
Write-Host " REST API:   https://cyhdocgapi.patnaorthoclinic.com" -ForegroundColor Magenta
Write-Host "==================================================" -ForegroundColor Magenta
