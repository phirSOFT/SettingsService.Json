# SettingsService Json Provider
[![Build Status](https://phirsoft.visualstudio.com/phirSOFT.SettingsService/_apis/build/status/phirSOFT.SettingsService.Json?branchName=master)](https://phirsoft.visualstudio.com/phirSOFT.SettingsService/_build/latest?definitionId=16&branchName=master)
[![Test Results](https://img.shields.io/azure-devops/tests/phirSOFT/phirSOFT.SettingsService/16)](https://phirsoft.visualstudio.com/phirSOFT.SettingsService/_build?definitionId=16)
![Azure DevOps coverage](https://img.shields.io/azure-devops/coverage/phirSOFT/phirSOFT.SettingsService/16)
![Nuget](https://img.shields.io/nuget/v/phirSOFT.SettingsService.Json)
[![License](https://img.shields.io/github/license/phirSOFT/SettingsService.Json)](https://github.com/phirSOFT/SettingsService.Json/blob/master/LICENSE)

This is a Settings backend for [phirSOFT/SettingsService](https://github.com/phirSOFT/SettingsService).

## Nuget ![Nuget (with prereleases)](https://img.shields.io/nuget/vpre/phirSOFT.SettingsService.Json)
This package is listed in the official [nuget.org](https://www.nuget.org/packages/phirSOFT.SettingsService.Json/) feed. You can install the latest release version by typing

> PM> Install-Package phirSOFT.SettingsService.Json

To retrieve development versions please add the development feed at https://phirsoft.pkgs.visualstudio.com/phirSOFT.SettingsService/_packaging/phirSOFT.SettingsServer/nuget/v3/index.json to your feed list or install the package directly from that feed.

> PM> Install-Package phirSOFT.SettingsService.Json -Source https://phirsoft.pkgs.visualstudio.com/phirSOFT.SettingsService/_packaging/phirSOFT.SettingsServer/nuget/v3/index.json

## Example
You can create easialy an instance of the `JsonSettingsService`

``` csharp
string path = "PathToSettingsFile.json"
ISettingsService settingsService = await JsonSettingsService.CreateAsync(path);
```