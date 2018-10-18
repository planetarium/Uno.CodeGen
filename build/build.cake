#addin "Cake.FileHelpers"
#addin "Cake.Powershell"
#tool "nuget:?package=GitVersion.CommandLine&version=3.6.5"

using System;
using System.Linq;
using System.Text.RegularExpressions;

//////////////////////////////////////////////////////////////////////
// ARGUMENTS
//////////////////////////////////////////////////////////////////////

var target = Argument("target", "Default");

//////////////////////////////////////////////////////////////////////
// VERSIONS
//////////////////////////////////////////////////////////////////////

var gitVersioningVersion = "2.0.41";
var signClientVersion = "0.9.0";

//////////////////////////////////////////////////////////////////////
// VARIABLES
//////////////////////////////////////////////////////////////////////

var baseDir = MakeAbsolute(Directory("../")).ToString();
var buildDir = baseDir + "/build";
var Solution = baseDir + "/src/Uno.CodeGen.sln";
GitVersion versionInfo = null;

//////////////////////////////////////////////////////////////////////
// METHODS
//////////////////////////////////////////////////////////////////////

void VerifyHeaders(bool Replace)
{
    var header = FileReadText("header.txt") + "\r\n";
    bool hasMissing = false;

    Func<IFileSystemInfo, bool> exclude_objDir =
        fileSystemInfo => !fileSystemInfo.Path.Segments.Contains("obj");

    var files = GetFiles(baseDir + "/**/*.cs", exclude_objDir).Where(file => 
    {
        var path = file.ToString();
        return !(path.EndsWith(".g.cs") || path.EndsWith(".i.cs") || System.IO.Path.GetFileName(path).Contains("TemporaryGeneratedFile"));
    });

    Information("\nChecking " + files.Count() + " file header(s)");
    foreach(var file in files)
    {
        var oldContent = FileReadText(file);
		if(oldContent.Contains("// <auto-generated>"))
		{
		   continue;
		}
        var rgx = new Regex("^(//.*\r?\n|\r?\n)*");
        var newContent = header + rgx.Replace(oldContent, "");

        if(!newContent.Equals(oldContent, StringComparison.Ordinal))
        {
            if(Replace)
            {
                Information("\nUpdating " + file + " header...");
                FileWriteText(file, newContent);
            }
            else
            {
                Error("\nWrong/missing header on " + file);
                hasMissing = true;
            }
        }
    }

    if(!Replace && hasMissing)
    {
        throw new Exception("Please run UpdateHeaders.bat or '.\\build.ps1 -target=UpdateHeaders' and commit the changes.");
    }
}

//////////////////////////////////////////////////////////////////////
// DEFAULT TASK
//////////////////////////////////////////////////////////////////////

Task("Build")
    .IsDependentOn("Version")
    .IsDependentOn("ValidateHeaders")
    .Description("Build all projects and get the assemblies")
    .Does(() =>
{
    Information("\nBuilding Solution");

    var buildSettings = new MSBuildSettings
    {
    }
    .SetConfiguration("Release")
    .WithProperty("PackageVersion", versionInfo.FullSemVer)
    .WithProperty("InformationalVersion", versionInfo.InformationalVersion)
    .WithProperty("PackageOutputPath", buildDir)
    .WithTarget("Restore")
    .WithTarget("Build")
    .WithTarget("Pack");
	
	MSBuild(Solution, buildSettings);
});


//////////////////////////////////////////////////////////////////////
// TASK TARGETS
//////////////////////////////////////////////////////////////////////

Task("Default")
    .IsDependentOn("Build");

Task("UpdateHeaders")
    .Description("Updates the headers in *.cs files")
    .Does(() =>
{
    VerifyHeaders(true);
});

Task("ValidateHeaders")
    .Description("Validates the headers in *.cs files")
    .Does(() =>
{
    VerifyHeaders(false);
});

Task("Version")
    .Description("Updates target versions")
    .Does(() =>
{
	versionInfo = GitVersion(new GitVersionSettings {
        UpdateAssemblyInfo = true,
		UpdateAssemblyInfoFilePath = baseDir + "/build/AssemblyVersion.cs"
    });

    Information($"FullSemVer: {versionInfo.FullSemVer} Sha: {versionInfo.Sha}");
});

//////////////////////////////////////////////////////////////////////
// EXECUTION
//////////////////////////////////////////////////////////////////////

RunTarget(target);