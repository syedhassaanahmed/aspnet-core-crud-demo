#tool "nuget:?package=GitVersion.CommandLine"

var target = Argument("target", "Default");
var outputDir = "./artifacts/";
var artifactName = "artifact.zip";
var solutionPath = "./AspNetCore.CrudDemo.sln";
var projectPath = "./AspNetCore.CrudDemo";
var buildConfig = "Release";

Task("Clean")
	.Does(() => 
	{
		if (DirectoryExists(outputDir))
			DeleteDirectory(outputDir, recursive:true);

		CreateDirectory(outputDir);
	});

Task("Restore")
	.Does(() => DotNetCoreRestore());

Task("Version")
	.Does(() => 
	{
		GitVersion(new GitVersionSettings
		{
			UpdateAssemblyInfo = true,
			OutputType = GitVersionOutput.BuildServer
		});

		var versionInfo = GitVersion(new GitVersionSettings{ OutputType = GitVersionOutput.Json });
		VersionProject(projectPath + "/project.json", versionInfo);
	});

private void VersionProject(string projectJsonPath, GitVersion versionInfo)
{
	var updatedProjectJson = System.IO.File.ReadAllText(projectJsonPath)
		.Replace("1.0.0-*", versionInfo.NuGetVersion);
	System.IO.File.WriteAllText(projectJsonPath, updatedProjectJson);
}

Task("Build")
	.IsDependentOn("Clean")
	.IsDependentOn("Version")
	.IsDependentOn("Restore")
	.Does(() => 
	{
		MSBuild(solutionPath, new MSBuildSettings 
		{
			Verbosity = Verbosity.Minimal,
			ToolVersion = MSBuildToolVersion.VS2015,
			Configuration = buildConfig,
			PlatformTarget = PlatformTarget.MSIL
		});
	});

Task("Test")
	.IsDependentOn("Build")
	.Does(() => 
	{
		DotNetCoreTest("./AspNetCore.CrudDemo.Controllers.Tests");

		// Because DocumentDB emulator is not present on Travis
		if (!BuildSystem.IsRunningOnTravisCI)
			DotNetCoreTest("./AspNetCore.CrudDemo.Services.Tests");
	});

Task("Publish")
	.IsDependentOn("Test")
	.Does(() => 
	{
		PublishProject(projectPath, artifactName);

		if (BuildSystem.IsRunningOnAppVeyor)
		{
			var files = GetFiles(artifactName);
			foreach(var file in files)
				AppVeyor.UploadArtifact(file.FullPath);
		}
	});

private void PublishProject(string projectPath, string artifactName)
{
	var settings = new DotNetCorePublishSettings
	{
		Configuration = buildConfig,
		OutputDirectory = outputDir
	};
				
	DotNetCorePublish(projectPath, settings);
	Zip(outputDir, artifactName);
}

Task("Default")
	.IsDependentOn("Publish");

RunTarget(target);