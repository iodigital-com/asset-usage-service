<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net48</TargetFramework>
    <AutoGenerateBindingRedirects>true</AutoGenerateBindingRedirects>
    <GenerateBindingRedirectsOutputType>true</GenerateBindingRedirectsOutputType>
    <LangVersion>8.0</LangVersion>
    <Nullable>enable</Nullable>

    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="coverlet.collector" Version="6.0.0" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.8.0" />
    <PackageReference Include="Moq" Version="4.20.72" />
    <PackageReference Include="Sitecore.Analytics" Version="10.4.1" />
    <PackageReference Include="Sitecore.FakeDb" Version="2.0.1" />
    <PackageReference Include="Sitecore.Kernel" Version="10.4.1" />
    <PackageReference Include="System.Text.Json" Version="6.0.10" />
    <PackageReference Include="xunit" Version="2.5.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.5.3" />
    <!-- Force an up-to-date Roslyn toolset so C# 8 / nullable is supported for a net48 SDK-style project -->
    <PackageReference Include="Microsoft.Net.Compilers.Toolset" Version="4.0.1" PrivateAssets="all" />
  </ItemGroup>

  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>

  <ItemGroup>
    <Folder Include="Perfomance\" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\iO.Sitecore.publishing\iO.Sitecore.publishing.csproj" />
  </ItemGroup>

</Project>