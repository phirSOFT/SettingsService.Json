# Version 0.2.0

There are no public chagnes in this version. However, since there were larger
breaking changes in the
[SettingService](https://github.com/phirSOFT/SettingsService) project, we had to
reflect
[them](https://github.com/phirSOFT/SettingsService/blob/0.2.0/ReleaseNotes.md)
here too. This especially includes using the new
[AbstractionsPackage](https://www.nuget.org/packages/phirSOFT.SettingsService.Abstraction).

Internally we now derive from the `CachedSettingsServiceBase`, which aims to
simplify the code.
