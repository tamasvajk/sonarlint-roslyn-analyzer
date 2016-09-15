
#cleanup
del %USERPROFILE%\AppData\Local\Microsoft\MSBuild\14.0\Microsoft.Common.targets\ImportBefore\SonarLint.Testing.ImportBefore.targets 

#nuget restore
& $env:NUGET_PATH restore SonarAnalyzer.sln

#build tests
& $env:MSBUILD_PATH /p:configuration=Release /p:DeployExtension=false /p:ZipPackageCompressionLevel=normal /v:m

#download nuget package
$ARTIFACTORY_SRC_REPO="sonarsource-nuget-qa"
$url = "$env:ARTIFACTORY_URL/$ARTIFACTORY_SRC_REPO/$env:FILENAME"
Write-Host "Downloading $url"
$pair = "$($env:REPOX_QAPUBLICADMIN_USERNAME):$($env:REPOX_QAPUBLICADMIN_PASSWORD)"
$encodedCreds = [System.Convert]::ToBase64String([System.Text.Encoding]::ASCII.GetBytes($pair))
$basicAuthValue = "Basic $encodedCreds"
$Headers = @{Authorization = $basicAuthValue}
Invoke-WebRequest -UseBasicParsing -Uri "$url" -Headers $Headers -OutFile $env:FILENAME


#unzip nuget package
$zipName=$env:FILENAME.Substring(0, $env:FILENAME.LastIndexOf('.'))+".zip"
Move-Item $env:FILENAME $zipName -force
$shell_app=new-object -com shell.application
$currentdir=(Get-Item -Path ".\" -Verbose).FullName
$destination = $shell_app.NameSpace($currentdir)
$zip_file = $shell_app.NameSpace("$currentdir\$zipName")
Write-Host "Unzipping $currentdir\$zipName"
$destination.CopyHere($zip_file.Items(), 0x14) 

#get sha1
$productversion=ls .\analyzers\SonarAnalyzer.dll | % { $_.versioninfo.productversion }
$sha1=$productversion.Substring($productversion.LastIndexOf('Sha1:')+5)
Write-Host "Checking out $sha1"

if (($env:GITHUB_BRANCH -eq "master") -or ($env:GITHUB_BRANCH -eq "refs/heads/master")) {
    $env:GITHUB_BRANCH=$env:GITHUB_BRANCH.Substring(11)
}

#checkout commit
git pull origin $env:GITHUB_BRANCH
git checkout -f $sha1

#move dlls to correct locations
Write-Host "Installing downloaded dlls"
Move-Item .\analyzers\*.dll .\src\SonarAnalyzer.CSharp\bin\Release -force

#run tests
Write-Host "Start tests"
& $env:VSTEST_PATH .\src\Tests\SonarAnalyzer.Platform.Integration.UnitTest\bin\Release\SonarAnalyzer.Platform.Integration.UnitTest.dll
& $env:VSTEST_PATH .\src\Tests\SonarAnalyzer.UnitTest\bin\Release\SonarAnalyzer.UnitTest.dll
 
#run regression-test
Write-Host "Start regression tests"
cd its
git submodule update --init --recursive --depth 1
cmd /c .\regression-test.bat