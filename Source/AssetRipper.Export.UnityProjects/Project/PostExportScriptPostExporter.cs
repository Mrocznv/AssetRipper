using AssetRipper.Export.Configuration;
using AssetRipper.Import.Configuration;
using AssetRipper.Import.Logging;
using AssetRipper.Processing;
using System.Diagnostics;

namespace AssetRipper.Export.UnityProjects.Project;

/// <summary>
/// Runs <see cref="Configuration.ExportSettings.PostExportScript"/> on the exported project, last, when it's set:
/// for example a game's own patch set, which makes the export ready to open in one go.
/// </summary>
/// <remarks>
/// The script gets the project folder (the one with Assets in it) as its only argument. A .ps1 script runs with
/// PowerShell, a .sh script with bash (Git Bash on Windows), anything else directly. Its output goes to the log. A missing
/// script or one that fails is logged and doesn't fail the export, which is already complete.
/// </remarks>
public class PostExportScriptPostExporter : IPostExporter
{
	public void DoPostExport(GameData gameData, FullConfiguration settings, FileSystem fileSystem)
	{
		string? script = settings.ExportSettings.PostExportScript;
		if (string.IsNullOrWhiteSpace(script))
		{
			return;
		}
		script = Path.GetFullPath(Environment.ExpandEnvironmentVariables(script));
		if (!File.Exists(script))
		{
			Logger.Warning(LogCategory.Export, $"Post-export script {script} doesn't exist, so it wasn't run.");
			return;
		}

		string project = Path.GetFullPath(settings.ProjectRootPath);
		ProcessStartInfo start = Path.GetExtension(script).ToLowerInvariant() switch
		{
			".ps1" => new ProcessStartInfo(OperatingSystem.IsWindows() ? "powershell" : "pwsh"),
			".sh" => new ProcessStartInfo("bash"),
			_ => new ProcessStartInfo(script),
		};
		if (start.FileName != script)
		{
			if (start.FileName.Contains("powershell", StringComparison.Ordinal) || start.FileName == "pwsh")
			{
				start.ArgumentList.Add("-NoProfile");
				start.ArgumentList.Add("-ExecutionPolicy");
				start.ArgumentList.Add("Bypass");
				start.ArgumentList.Add("-File");
			}
			start.ArgumentList.Add(script);
		}
		start.ArgumentList.Add(project);
		start.WorkingDirectory = Path.GetDirectoryName(script);
		start.UseShellExecute = false;
		start.RedirectStandardOutput = true;
		start.RedirectStandardError = true;

		Logger.Info(LogCategory.Export, $"Running post-export script {script} on {project}...");
		try
		{
			using Process process = new() { StartInfo = start };
			process.OutputDataReceived += (_, e) => { if (e.Data is not null) Logger.Info(LogCategory.Export, "  " + e.Data); };
			process.ErrorDataReceived += (_, e) => { if (e.Data is not null) Logger.Warning(LogCategory.Export, "  " + e.Data); };
			process.Start();
			process.BeginOutputReadLine();
			process.BeginErrorReadLine();
			process.WaitForExit();
			if (process.ExitCode == 0)
			{
				Logger.Info(LogCategory.Export, "Post-export script finished.");
			}
			else
			{
				Logger.Error(LogCategory.Export, $"Post-export script failed (exit code {process.ExitCode}); the export itself is complete.");
			}
		}
		catch (Exception exception)
		{
			Logger.Error(LogCategory.Export, $"Couldn't run the post-export script {script}: {exception.Message}");
		}
	}
}
