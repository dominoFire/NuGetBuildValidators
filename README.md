# NuGetBuildValidators

## Introduction
This is a tool used to validate the localized strings for NuGet.Tools.vsix

## Instructions

* git clone https://github.com/mishra14/NuGetBuildValidators.git
* cd NuGetBuildValidators
* `msbuild`
* `.\bin\Debug\NuGetStringChecker.exe "\\wsr-tc\Drops\NuGet.Signed.AllLanguages\latest-successful\Signed\VSIX\15\NuGet.Tools.vsix" "\\nuget\NuGet\Share\ValidationTemp\NuGet.Tools.Vsix" "\\nuget\NuGet\Share\ValidationTemp\errors.txt"`
