git config --global user.email "$($env:GitEmail)"
git config --global user.name "$($env:GitUsername)"
git config --global credential.helper store

Add-Content "$env:USERPROFILE\.git-credentials" "https://$($env:GitUsername):$($env:GitPassword)@bitbucket.org`n"

git remote add bitbucket https://$($env:GitUsername)@bitbucket.org/$($env:APPVEYOR_REPO_NAME).git
git tag $($env:appveyor_build_version) $($env:APPVEYOR_REPO_COMMIT)
git push bitbucket --tags --quiet
