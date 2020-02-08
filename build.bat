@echo off
set /p version="Enter Version Number to Build With: "

@echo on
dotnet pack ".\BDTest\BDTest.csproj"  --configuration Release /p:Version=%version% /p:NuspecFile="BDTest.nuspec" 

dotnet pack ".\BDTest.NUnit\BDTest.NUnit.csproj"  --configuration Release /p:Version=%version% /p:NuspecFile="BDTest.NUnit.nuspec"

dotnet pack ".\BDTest.ReportGenerator\BDTest.ReportGenerator.csproj" --configuration Release /p:Version=%version% /p:NuspecFile="BDTest.ReportGenerator.nuspec"

pause