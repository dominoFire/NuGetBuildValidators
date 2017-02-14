# NuGetBuildValidators

## Introduction
This is a tool used to validate the localized strings for NuGet.Tools.vsix

## Instructions

* git clone https://github.com/mishra14/NuGetBuildValidators.git
* cd NuGetBuildValidators
* `msbuild`
* `.\NuGetValidators.Localization\bin\Debug\NuGetValidator.Localization.exe "Path\to\vsix\NuGet.Tools.vsix" "Path\to\extract\NuGet.Tools.Vsix" "Path\to\log\"`

## Output
Output summary is displayed on the console. The tool generates 3 logs indicating different types of failures.
