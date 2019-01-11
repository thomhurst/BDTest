dotnet build "./BDTest/BDTest.csproj"
dotnet build "./BDTest/BDTest.csproj" --configuration Release
nuget pack "./BDTest/BDTest.csproj"

dotnet build "./BDTest.ReportGenerator/BDTest.ReportGenerator.csproj"
dotnet build "./BDTest.ReportGenerator/BDTest.ReportGenerator.csproj" --configuration Release
nuget pack "./BDTest.ReportGenerator/BDTest.ReportGenerator.nuspec"

pause