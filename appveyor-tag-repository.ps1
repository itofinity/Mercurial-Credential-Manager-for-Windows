git config --global user.email "$($env:GitEmail)"
git config --global user.name "$($env:GitUsername)"
git config --global credential.helper store

Add-Content "$env:USERPROFILE\.git-credentials" "https://$($env:GitUsername):$($env:GitPassword)@github.com`n"

git remote add github https://$($env:GitUsername)@github.com/mminns/Mercurial-Credential-Manager-for-Windows.git
git tag "v$($env:appveyor_build_version)" $($env:APPVEYOR_REPO_COMMIT)
git tag "v$($env:appveyor_build_version).ci" $($env:APPVEYOR_REPO_COMMIT)
git push github --tags --quiet
