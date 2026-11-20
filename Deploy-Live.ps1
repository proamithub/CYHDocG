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

function Ensure-FtpDirectory($ftpDirUri) {
    try {
        $req = [System.Net.FtpWebRequest]::Create($ftpDirUri)
        $req.Method = [System.Net.WebRequestMethods+Ftp]::MakeDirectory
        $req.Credentials = New-Object System.Net.NetworkCredential($ftpUser, $ftpPass)
        $req.Timeout = 10000
        $res = $req.GetResponse()
        $res.Close()
    } catch {
        # Directory might already exist, ignore
    }
}

function Upload-FtpFolder($localDir, $remoteUri) {
    Write-Host "`n>>> Starting Upload from: $localDir" -ForegroundColor Cyan
    Write-Host ">>> Target Destination: $remoteUri" -ForegroundColor Cyan

    $allFiles = Get-ChildItem -Path $localDir -Recurse -File
    $total = $allFiles.Count
    $current = 0

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
            $req.Timeout = 20000

            $fileBytes = [System.IO.File]::ReadAllBytes($file.FullName)
            $req.ContentLength = $fileBytes.Length

            $stream = $req.GetRequestStream()
            $stream.Write($fileBytes, 0, $fileBytes.Length)
            $stream.Close()

            $res = $req.GetResponse()
            $res.Close()

            Write-Host "[$current/$total] Uploaded: $relative" -ForegroundColor Green
        } catch {
            Write-Host "[$current/$total] Failed: $relative - $($_.Exception.Message)" -ForegroundColor Red
        }
    }

    Write-Host ">>> Upload complete for: $remoteUri`n" -ForegroundColor Yellow
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

Write-Host "All tasks completed!" -ForegroundColor Cyan
