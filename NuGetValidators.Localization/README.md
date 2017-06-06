# NuGetBuildValidators.Localization

## Introduction

This is a tool used to validate the localized strings for NuGet.Tools.vsix.


## Usage

The tool can be used in a few different ways -

### Using NuGetValidator.Localization.exe

The NuGetValidator.Localization.exe requires the following arguments - 

```
arg[0]: NuGet.Tools.Vsix path
arg[1]: Path to extract NuGet.Tools.Vsix into. 
  Folder need not be present, but Program should have write access to the location.
arg[2]: Path to the directory for writing errors. 
  Folder need not be present, but Program should have write access to the location.
arg[3]: Path to the local NuGet Localization repository. 
  e.g. - <NuGet_Localization_repository>\Main\localize\comments\15
```
NuGet Localization repository - https://github.com/NuGet/NuGet.Build.Localization

* `git clone https://github.com/mishra14/NuGetBuildValidators.git`
* `cd NuGetBuildValidators`
* `cd NuGetValidators.Localization`
* `msbuild /t:Restore`
* `msbuild`
* `.\NuGetValidators.Localization\bin\Debug\net45\NuGetValidator.Localization.exe "Path\to\vsix\NuGet.Tools.vsix" "Path\to\extract\NuGet.Tools.Vsix" "Path\to\log\" "<NuGet_Localization_repository>\Main\localize\comments\15"`


### Using build.ps1

* `git clone https://github.com/mishra14/NuGetBuildValidators.git`
* `cd NuGetBuildValidators`
* `cd NuGetValidators.Localization`
* `msbuild /t:Restore`
* `msbuild`
* `.\build.ps1 -VS15InsVSIXPath "Path\to\vsix\NuGet.Tools.vsix" -VSIXUnzipPath "Path\to\extract\NuGet.Tools.Vsix" -LogPath "Path\to\log\" -NuGetCommentsPath "<NuGet_Localization_repository>\Main\localize\comments\15"`


NuGet Localization repository - https://github.com/NuGet/NuGet.Build.Localization


### Using NuGet Package NuGetValidator.Localization.nupkg

Things in flight, these instructions will be added soon....

## Output

Output summary is displayed on the console. The tool generates multiple logs indicating different types of failures. The summry on the console displays the type of failure and the corresponding log file.
