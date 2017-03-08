# NuGetBuildValidators

## Introduction
This is a tool used to validate the localized strings for NuGet.Tools.vsix

## Instructions

* git clone https://github.com/mishra14/NuGetBuildValidators.git
* cd NuGetBuildValidators
* `msbuild`
* `.\NuGetValidators.Localization\bin\Debug\NuGetValidator.Localization.exe "Path\to\vsix\NuGet.Tools.vsix" "Path\to\extract\NuGet.Tools.Vsix" "Path\to\log\"`

## Output
Output summary is displayed on the console. The tool generates multiple logs indicating different types of failures. The summry on the console displays the type of failure and the corresponding log file.
