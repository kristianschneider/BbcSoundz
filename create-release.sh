#!/bin/bash

# Release Helper Script for BBC Soundz
# Usage: ./create-release.sh [version] [message]
# Example: ./create-release.sh 1.0.0 "Initial release with multi-source scraping"

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Function to print colored output
print_status() {
    echo -e "${BLUE}[INFO]${NC} $1"
}

print_success() {
    echo -e "${GREEN}[SUCCESS]${NC} $1"
}

print_warning() {
    echo -e "${YELLOW}[WARNING]${NC} $1"
}

print_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

# Check if version is provided
if [ $# -lt 1 ]; then
    print_error "Usage: $0 <version> [release-message]"
    print_error "Example: $0 1.0.0 'Initial release with multi-source scraping'"
    exit 1
fi

VERSION=$1
RELEASE_MESSAGE=${2:-"Release version $VERSION"}

# Validate version format (semantic versioning)
if ! [[ $VERSION =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
    print_error "Version must be in semantic versioning format (e.g., 1.0.0)"
    exit 1
fi

print_status "Creating release for version $VERSION"

# Check if we're in a git repository
if ! git rev-parse --git-dir > /dev/null 2>&1; then
    print_error "Not in a git repository"
    exit 1
fi

# Check if working directory is clean
if [ -n "$(git status --porcelain)" ]; then
    print_warning "Working directory is not clean. Uncommitted changes:"
    git status --short
    read -p "Continue anyway? (y/N): " -n 1 -r
    echo
    if [[ ! $REPLY =~ ^[Yy]$ ]]; then
        print_error "Aborted by user"
        exit 1
    fi
fi

# Update version in project file if it exists
PROJECT_FILE="BbcSoundz.csproj"
if [ -f "$PROJECT_FILE" ]; then
    print_status "Updating version in $PROJECT_FILE"
    # This would update version in .csproj if version tags exist
    # For now, we'll just continue
fi

# Create and push tag
TAG_NAME="v$VERSION"

print_status "Creating git tag: $TAG_NAME"
git tag -a "$TAG_NAME" -m "$RELEASE_MESSAGE"

print_status "Pushing tag to remote repository"
git push origin "$TAG_NAME"

print_success "Release tag $TAG_NAME created and pushed!"
print_status "GitHub Actions will now build and create the release automatically."
print_status "Check the Actions tab in your GitHub repository for progress."
print_status "Release will be available at: https://github.com/$(git config --get remote.origin.url | sed 's/.*github.com[:/]\([^.]*\).*/\1/')/releases/tag/$TAG_NAME"

# Optionally open the releases page
if command -v start > /dev/null 2>&1; then
    read -p "Open releases page in browser? (y/N): " -n 1 -r
    echo
    if [[ $REPLY =~ ^[Yy]$ ]]; then
        start "https://github.com/$(git config --get remote.origin.url | sed 's/.*github.com[:/]\([^.]*\).*/\1/')/releases"
    fi
fi
