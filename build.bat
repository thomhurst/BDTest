@echo off
set /p version="Enter Version Number to Build With: "

@echo on

dotnet build "C:\git\BDTest\BDTest\BDTest.csproj" /p:AssemblyVersion=%version% /p:Version=%version%
dotnet build "C:\git\BDTest\BDTest\BDTest.csproj" --configuration Release /p:AssemblyVersion=%version% /p:Version=%version%
powershell -File "C:\git\BDTest\findreplace.ps1" -FilePath "C:\git\BDTest\BDTest\BDTest.nuspec" -Find "###version###" -Replace "%version%"
dotnet pack "C:\git\BDTest\BDTest\BDTest.csproj" /p:NuspecFile="C:\git\BDTest\BDTest\BDTest.nuspec" --configuration Release --output "C:\git\BDTest" --no-build
powershell -File "C:\git\BDTest\findreplace.ps1" -FilePath "C:\git\BDTest\BDTest\BDTest.nuspec" -Find "%version%" -Replace "###version###"

dotnet build "C:\git\BDTest\BDTest.NUnit\BDTest.NUnit.csproj" /p:AssemblyVersion=%version% /p:Version=%version%
dotnet build "C:\git\BDTest\BDTest.NUnit\BDTest.NUnit.csproj" --configuration Release /p:AssemblyVersion=%version% /p:Version=%version%
powershell -File "C:\git\BDTest\findreplace.ps1" -FilePath "C:\git\BDTest\BDTest.NUnit\BDTest.NUnit.nuspec" -Find "###version###" -Replace "%version%"
dotnet pack "C:\git\BDTest\BDTest.NUnit\BDTest.NUnit.csproj" /p:NuspecFile="C:\git\BDTest\BDTest.NUnit\BDTest.NUnit.nuspec" --configuration Release --output "C:\git\BDTest" --no-build
powershell -File "C:\git\BDTest\findreplace.ps1" -FilePath "C:\git\BDTest\BDTest.NUnit\BDTest.NUnit.nuspec" -Find "%version%" -Replace "###version###"

dotnet build "C:\git\BDTest\BDTest.ReportGenerator\BDTest.ReportGenerator.csproj" /p:AssemblyVersion=%version% /p:Version=%version%
dotnet build "C:\git\BDTest\BDTest.ReportGenerator\BDTest.ReportGenerator.csproj" --configuration Release /p:AssemblyVersion=%version% /p:Version=%version%
powershell -File "C:\git\BDTest\findreplace.ps1" -FilePath "C:\git\BDTest\BDTest.ReportGenerator\BDTest.ReportGenerator.nuspec" -Find "###version###" -Replace "%version%"
dotnet pack "C:\git\BDTest\BDTest.ReportGenerator\BDTest.ReportGenerator.csproj" /p:NuspecFile="C:\git\BDTest\BDTest.ReportGenerator\BDTest.ReportGenerator.nuspec" --configuration Release --output "C:\git\BDTest" --no-build
powershell -File "C:\git\BDTest\findreplace.ps1" -FilePath "C:\git\BDTest\BDTest.ReportGenerator\BDTest.ReportGenerator.nuspec" -Find "%version%" -Replace "###version###"

pause