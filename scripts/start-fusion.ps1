
New-Item -Path "HKLM:\SOFTWARE\Microsoft\Fusion" -Force | Out-Null
Set-ItemProperty -Path "HKLM:\SOFTWARE\Microsoft\Fusion" -Name "EnableLog" -Value 1 -Type DWord
Set-ItemProperty -Path "HKLM:\SOFTWARE\Microsoft\Fusion" -Name "ForceLog" -Value 1 -Type DWord
Set-ItemProperty -Path "HKLM:\SOFTWARE\Microsoft\Fusion" -Name "LogFailures" -Value 1 -Type DWord
Set-ItemProperty -Path "HKLM:\SOFTWARE\Microsoft\Fusion" -Name "LogResourceBinds" -Value 1 -Type DWord
Set-ItemProperty -Path "HKLM:\SOFTWARE\Microsoft\Fusion" -Name "LogPath" -Value "D:\FusionLogs" -Type String
