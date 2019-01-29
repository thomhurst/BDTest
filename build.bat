dotnet build "C:\git\BDTest\BDTest\BDTest.csproj"
dotnet build "C:\git\BDTest\BDTest\BDTest.csproj" --configuration Release
dotnet pack "C:\git\BDTest\BDTest\BDTest.csproj" /p:NuspecFile="C:\git\BDTest\BDTest\BDTest.nuspec" --configuration Release --output "C:\git\BDTest" --no-build

dotnet build "C:\git\BDTest\BDTest.NUnit\BDTest.NUnit.csproj"
dotnet build "C:\git\BDTest\BDTest.NUnit\BDTest.NUnit.csproj" --configuration Release
dotnet pack "C:\git\BDTest\BDTest.NUnit\BDTest.NUnit.csproj" /p:NuspecFile="C:\git\BDTest\BDTest.NUnit\BDTest.NUnit.nuspec" --configuration Release --output "C:\git\BDTest" --no-build

dotnet build "C:\git\BDTest\BDTest.ReportGenerator\BDTest.ReportGenerator.csproj"
dotnet build "C:\git\BDTest\BDTest.ReportGenerator\BDTest.ReportGenerator.csproj" --configuration Release
dotnet pack "C:\git\BDTest\BDTest.ReportGenerator\BDTest.ReportGenerator.csproj" /p:NuspecFile="C:\git\BDTest\BDTest.ReportGenerator\BDTest.ReportGenerator.nuspec" --configuration Release --output "C:\git\BDTest" --no-build

pause