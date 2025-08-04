# Release Helper Script for BBC Soundz
# Usage: .\create-release.ps1 [version] [message]
# Example: .\create-release.ps1 1.0.0 "Initial release with multi-source scraping"

param(
    [Parameter(Mandatory=$true)]
    [string]$Version,
    
    [Parameter(Mandatory=$false)]
    [string]$ReleaseMessage = "Release version $Version"
)

# Function to write colored output
function Write-Status {
    param([string]$Message)
    Write-Host "[INFO] $Message" -ForegroundColor Blue
}

function Write-Success {
    param([string]$Message)
    Write-Host "[SUCCESS] $Message" -ForegroundColor Green
}

function Write-Warning {
    param([string]$Message)
    Write-Host "[WARNING] $Message" -ForegroundColor Yellow
}

function Write-Error {
    param([string]$Message)
    Write-Host "[ERROR] $Message" -ForegroundColor Red
}

# Validate version format (semantic versioning)
if ($Version -notmatch '^[0-9]+\.[0-9]+\.[0-9]+$') {
    Write-Error "Version must be in semantic versioning format (e.g., 1.0.0)"
    exit 1
}

Write-Status "Creating release for version $Version"

# Check if we're in a git repository
try {
    git rev-parse --git-dir | Out-Null
} catch {
    Write-Error "Not in a git repository"
    exit 1
}

# Check if working directory is clean
$gitStatus = git status --porcelain
if ($gitStatus) {
    Write-Warning "Working directory is not clean. Uncommitted changes:"
    git status --short
    $continue = Read-Host "Continue anyway? (y/N)"
    if ($continue -notmatch '^[Yy]$') {
        Write-Error "Aborted by user"
        exit 1
    }
}

# Update version in project file if it exists
$projectFile = "BbcSoundz.csproj"
if (Test-Path $projectFile) {
    Write-Status "Project file found: $projectFile"
    # Could update version in .csproj if needed
}

# Create and push tag
$tagName = "v$Version"

Write-Status "Creating git tag: $tagName"
try {
    git tag -a $tagName -m $ReleaseMessage
    
    Write-Status "Pushing tag to remote repository"
    git push origin $tagName
    
    Write-Success "Release tag $tagName created and pushed!"
    Write-Status "GitHub Actions will now build and create the release automatically."
    Write-Status "Check the Actions tab in your GitHub repository for progress."
    
    # Get repository URL
    $remoteUrl = git config --get remote.origin.url
    $repoPath = ""
    if ($remoteUrl -match 'github\.com[:/]([^.]+)') {
        $repoPath = $matches[1]
        $releaseUrl = "https://github.com/$repoPath/releases/tag/$tagName"
        Write-Status "Release will be available at: $releaseUrl"
        
        # Optionally open the releases page
        $openBrowser = Read-Host "Open releases page in browser? (y/N)"
        if ($openBrowser -match '^[Yy]$') {
            Start-Process $releaseUrl
        }
    }
    
} catch {
    Write-Error "Failed to create or push tag: $_"
    exit 1
}
