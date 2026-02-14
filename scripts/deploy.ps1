param(
    [string]$action = "help"
)

function Show-Help {
    Write-Host "Usage: .\deploy.ps1 build|package|deploy"
    Write-Host "Requires: dotnet, AWS CLI, and optionally Amazon.Lambda.Tools"
}

if ($action -eq "help") { Show-Help; return }

if ($action -eq "build") {
    dotnet restore
    dotnet build -c Release
    dotnet publish src/Accounting.ProcessorLambda -c Release -o ./artifacts/processor
    dotnet publish src/Accounting.IngestLambda -c Release -o ./artifacts/ingest
    Write-Host "Built. Artifacts in ./artifacts"
    return
}

if ($action -eq "package") {
    if (Test-Path ./artifacts/processor) {
        Compress-Archive -Path ./artifacts/processor/* -DestinationPath ./artifacts/processor.zip -Force
    }
    if (Test-Path ./artifacts/ingest) {
        Compress-Archive -Path ./artifacts/ingest/* -DestinationPath ./artifacts/ingest.zip -Force
    }
    Write-Host "Packaged zips in ./artifacts"
    return
}

if ($action -eq "deploy") {
    Write-Host "Deploy is environment-specific. Use AWS Console or 'dotnet lambda deploy-function' with the appropriate role ARN."
    return
}

Show-Help

