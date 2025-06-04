# GitHub Preparation Checklist

Before pushing this project to GitHub, ensure you've completed the following steps:

## Security Checks

- [ ] Run the cleaning script with `./clean-for-github.ps1`
- [ ] Verify all configuration files have been sanitized:
  - [ ] MCP.Client/wwwroot/appsettings.json
  - [ ] MCP.Server/appsettings.json
  - [ ] MCP.Server/appsettings.Development.json
- [ ] Verify documentation files have been sanitized:
  - [ ] GraphApiPermissionFix.md
  - [ ] TestGraphPermissions.ps1
  - [ ] README.md
- [ ] Check for any additional files with hardcoded secrets or credentials
- [ ] Ensure no .private.bak files are included in the commit

## Project Structure

- [ ] Remove any unnecessary build artifacts:
  - [ ] bin/
  - [ ] obj/
  - [ ] __blobstorage__/
  - [ ] .vs/
- [ ] Ensure .gitignore is correctly set up
- [ ] Verify .gitattributes is present

## Documentation

- [ ] README.md is comprehensive and updated
- [ ] CONTRIBUTING.md is included
- [ ] LICENSE file is present
- [ ] Setup instructions are clear

## Final Verification

- [ ] Run `git status` to check for any untracked files that should be ignored
- [ ] Run `git diff` to inspect changes and ensure no secrets are included
- [ ] Consider creating a new branch for the GitHub version

Once all items are checked, you're ready to push your code to GitHub!
