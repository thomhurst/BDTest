@echo off
set /p version="Enter Version Number to Build With: "

@echo on
dotnet pack ".\BDTest\BDTest.csproj"  --configuration Release /p:Version=%version%

dotnet pack ".\BDTest.NUnit\BDTest.NUnit.csproj"  --configuration Release /p:Version=%version%

dotnet pack ".\BDTest.ReportGenerator\BDTest.ReportGenerator.csproj" --configuration Release /p:Version=%version%

pause