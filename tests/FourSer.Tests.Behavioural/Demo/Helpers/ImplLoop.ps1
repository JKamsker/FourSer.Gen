[CmdletBinding()]
param(
    [string]$ProjectPath = (Join-Path (Join-Path $PSScriptRoot '..\..') 'FourSer.Tests.Behavioural.csproj'),
    [string]$TestFilter = 'FullyQualifiedName=FourSer.Tests.Behavioural.Demo.TcdRoundTripTests.File_Has_Serializer',
    [int]$MaxAttempts = 5,
    [switch]$DryRun
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Invoke-TcdTests {
    param(
        [string]$Project,
        [string]$Filter,
        [switch]$SkipBuild
    )

    $arguments = @('test', $Project, '--filter', $Filter)
    if ($SkipBuild) {
        $arguments += @('--no-restore', '--no-build')
    }

    Write-Host "Running: dotnet $($arguments -join ' ')" -ForegroundColor Cyan

    $output = & dotnet @arguments 2>&1
    $exitCode = $LASTEXITCODE

    return [PSCustomObject]@{
        ExitCode = $exitCode
        Output   = $output
    }
}

function Get-MissingSerializerNames {
    param([string[]]$TestOutput)

    $joined = $TestOutput -join [Environment]::NewLine
    $pattern = "Resource '([^']+)' is not mapped to a serializer\."

    $matches = [regex]::Matches($joined, $pattern)
    if ($matches.Count -eq 0) {
        return @()
    }

    return @($matches | ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique)
}

function Invoke-CodexPrompt {
    param(
        [string]$Prompt,
        [string]$WorkingDirectory,
        [switch]$DryRun
    )

    Write-Host "Preparing to call Codex with prompt:" -ForegroundColor Yellow
    Write-Host $Prompt -ForegroundColor DarkYellow

    if ($DryRun) {
        Write-Host "DryRun specified; skipping Codex invocation." -ForegroundColor DarkYellow
        return 0
    }
    
    $codexArgs = @('exec', '--yolo' ,'--cd', $WorkingDirectory, $Prompt)
    Write-Host "Invoking Codex: codex $($codexArgs -join ' ')" -ForegroundColor Cyan
    & codex @codexArgs
    return $LASTEXITCODE
}

$resolvedProject = (Resolve-Path -LiteralPath $ProjectPath).Path
$repoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\..\..\..')).Path

Write-Host "Project: $resolvedProject" -ForegroundColor Yellow
Write-Host "Repository root: $repoRoot" -ForegroundColor Yellow

$attempt = 0
while ($attempt -lt $MaxAttempts) {
    $attempt++
    Write-Host "Attempt $attempt of $MaxAttempts" -ForegroundColor Green

    $skipBuild = $attempt -gt 1
    $result = Invoke-TcdTests -Project $resolvedProject -Filter $TestFilter -SkipBuild:$skipBuild
    $result.Output | ForEach-Object { Write-Host $_ }

    if ($result.ExitCode -eq 0) {
        Write-Host "TcdRoundTripTests.File_Has_Serializer passed." -ForegroundColor Green
        exit 0
    }

    $missing = @(Get-MissingSerializerNames -TestOutput $result.Output)
    if ($missing.Count -eq 0) {
        Write-Host "Tests failed before reaching missing serializer diagnostics. Attempting Codex-assisted fix." -ForegroundColor Magenta
        $logExcerpt = ($result.Output | Select-Object -Last 200) -join [Environment]::NewLine
        $promptLines = @(
            ("Fix the build/test failure for ``tests\FourSer.Tests.Behavioural\FourSer.Tests.Behavioural.csproj`` so that ``dotnet test --filter {0}`` executes far enough to report serializer coverage issues. Use the diagnostics below to address the problem, then rerun the tests." -f $TestFilter),
            '',
            'Latest test log:',
            '```',
            $logExcerpt,
            '```'
        )
        $prompt = $promptLines -join [Environment]::NewLine

        $codexExit = Invoke-CodexPrompt -Prompt $prompt -WorkingDirectory $repoRoot -DryRun:$DryRun
        if ($codexExit -ne 0) {
            Write-Host "Codex invocation failed with exit code $codexExit. Halting." -ForegroundColor Red
            exit $codexExit
        }

        continue
    }

    Write-Host "Missing serializers detected:" -ForegroundColor Red
    $missing | ForEach-Object { Write-Host " - $_" -ForegroundColor Red }

    $missingLines = $missing | ForEach-Object { "- $_" }
    $promptLines = @(
        'Generate serializers in ``tests\FourSer.Tests.Behavioural\Demo\Readers`` for the following missing tcd files. Consult the readme at ``tests\FourSer.Tests.Behavioural\Demo\README.md`` for more information.',
        '',
        'Missing files:',
        ($missingLines -join [Environment]::NewLine)
    )
    $prompt = $promptLines -join [Environment]::NewLine

    $codexExit = Invoke-CodexPrompt -Prompt $prompt -WorkingDirectory $repoRoot -DryRun:$DryRun
    if ($codexExit -ne 0) {
        Write-Host "Codex invocation failed with exit code $codexExit. Halting." -ForegroundColor Red
        exit $codexExit
    }
}

Write-Host "Reached maximum attempts ($MaxAttempts) without a passing test run." -ForegroundColor Red
exit 1
