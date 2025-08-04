# Release Checklist for BBC Soundz

## Pre-Release Checklist

### Code Quality
- [ ] All features working as expected
- [ ] No known critical bugs
- [ ] Code has been tested on target Windows versions
- [ ] All dependencies are properly included
- [ ] yt-dlp.exe is included and working
- [ ] appsettings.json has correct default configuration

### Documentation
- [ ] README.md is up to date
- [ ] Version-specific changes documented
- [ ] Configuration instructions are clear
- [ ] Installation instructions are accurate

### Build Verification
- [ ] Project builds successfully in Release configuration
- [ ] Self-contained build works without .NET runtime
- [ ] Framework-dependent build works with .NET 9.0
- [ ] All required files are included in output

### Version Management
- [ ] Version number follows semantic versioning (MAJOR.MINOR.PATCH)
- [ ] Version number is appropriate for changes made
- [ ] Release notes prepared

## Release Process

### 1. Prepare Release
```bash
# Ensure working directory is clean
git status

# Make sure you're on the main branch
git checkout main
git pull origin main
```

### 2. Create Release Tag
Use the helper script:

**Windows (PowerShell):**
```powershell
.\create-release.ps1 1.0.0 "Initial release with multi-source BBC scraping"
```

**Linux/Mac/WSL (Bash):**
```bash
./create-release.sh 1.0.0 "Initial release with multi-source BBC scraping"
```

**Manual approach:**
```bash
# Create and push tag
git tag -a v1.0.0 -m "Release version 1.0.0"
git push origin v1.0.0
```

### 3. Monitor GitHub Actions
- [ ] Check Actions tab in GitHub repository
- [ ] Verify build workflow completes successfully
- [ ] Verify release workflow creates release with assets

### 4. Verify Release
- [ ] Release appears in GitHub Releases section
- [ ] Both zip files are attached (standalone and framework-dependent)
- [ ] Checksums file is attached
- [ ] Release notes are properly formatted
- [ ] Download links work correctly

### 5. Test Release
- [ ] Download standalone version
- [ ] Extract and run on clean Windows machine
- [ ] Verify all features work
- [ ] Test with different BBC sources
- [ ] Verify download functionality

## Post-Release

### Announcement
- [ ] Update any external documentation
- [ ] Notify users of new release (if applicable)
- [ ] Consider posting on relevant forums/communities

### Version Planning
- [ ] Plan next version features
- [ ] Update project roadmap
- [ ] Create issues for known bugs/improvements

## Versioning Guidelines

### Semantic Versioning (MAJOR.MINOR.PATCH)

**MAJOR** version when you make incompatible API changes:
- Breaking changes to appsettings.json format
- Removal of features
- Major UI overhauls

**MINOR** version when you add functionality in a backwards compatible manner:
- New BBC sources
- New features
- UI improvements
- Performance enhancements

**PATCH** version when you make backwards compatible bug fixes:
- Bug fixes
- Security patches
- Minor UI fixes
- Configuration updates

## Emergency Release Process

If critical bugs are discovered:

1. Create hotfix branch from latest release tag
2. Fix the issue
3. Create new patch version (e.g., 1.0.0 → 1.0.1)
4. Follow normal release process
5. Merge hotfix back to main branch

## Rollback Process

If a release has critical issues:

1. Delete the problematic tag and release from GitHub
2. Fix the issues
3. Create new release with incremented version
4. Document the issue in release notes
