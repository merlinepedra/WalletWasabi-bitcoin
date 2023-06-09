using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace WalletWasabi.Packager;

public static class Tools
{
	public static void ClearSha512Tags(string pathToSearch)
	{
		var files = Directory.GetFiles(pathToSearch, "*.deps.json"); // https://natemcmaster.com/blog/2017/12/21/netcore-primitives/
		if (files is null)
		{
			return;
		}

		foreach (var depsFilePath in files)
		{
			var lines = File.ReadAllLines(depsFilePath);

			List<string> outLines = new();
			foreach (var line in lines)
			{
				// "sha512": "sha512-B0BYh5Fpeqp4GIbL5wEhde6M/dZ+s0tlXM0qMTvj4mTg9Rr4svVHGpn6dDp8pT2sB88ghxyLIpKGdx9Oj7f/pw==",
				if (line.Contains("\"sha512\": \"sha512-"))
				{
					// "sha512": "",
					var lineToAdd = "      \"sha512\": \"\"";
					if (line.EndsWith(','))
					{
						lineToAdd += ',';
					}

					outLines.Add(lineToAdd);
				}
				else
				{
					outLines.Add(line);
				}
			}
			File.Delete(depsFilePath);
			File.WriteAllLines(depsFilePath, outLines);
		}
	}

	public static string LinuxPathCombine(params string[] paths)
	{
		return LinuxPath(Path.Combine(paths));
	}

	public static string LinuxPath(string path)
	{
		return path.Replace(@"\", @"/");
	}

	/// <summary>Converts a Windows path to a WSL path.</summary>
	/// <param name="windowsPath">Full Windows path (e.g. <c>C:\something\something</c>).</param>
	/// <param name="driveLetterUpper">Output parameter with capital drive letter of <paramref name="windowsPath"/>.</param>
	/// <returns>WSL path corresponding with <paramref name="windowsPath"/> (e.g. <c>/mnt/c/something/something</c>).</returns>
	/// <remarks>Supports both WSL 1 and WSL 2.</remarks>
	public static string Win2WslPath(string windowsPath, out char driveLetterUpper)
	{
		driveLetterUpper = char.ToUpper(windowsPath[0]);
		return $"/mnt/{char.ToLower(driveLetterUpper)}/{LinuxPath(windowsPath[3..])}";
	}

	/// <summary>Builds a WSL command from <paramref name="commands"/> in a way that <c>chmod</c> command works in WSL (not true by default).</summary>
	/// <param name="windowsWorkingDirectory">Working directory where to run <paramref name="commands"/> as a Windows full path.</param>
	/// <param name="commands">Linux commands to execute in WSL folder corresponding to <paramref name="windowsWorkingDirectory"/>.</param>
	public static string CreateWslCommand(string windowsWorkingDirectory, params string[] commands)
	{
		string wslPath = Win2WslPath(windowsWorkingDirectory, out char driveLetterUpper);
		char driveLetterLower = char.ToLower(driveLetterUpper);

		string[] allCommands = new string[]
		{
			$"cd /",
			$"sudo umount -l /mnt/{driveLetterLower}",
			$"sudo mount -t drvfs {driveLetterUpper}: /mnt/{driveLetterLower} -o metadata",
			$"cd {wslPath}",
		}.Concat(commands).ToArray();

		return string.Join(" && ", allCommands);
	}

	public static long DirSize(DirectoryInfo d)
	{
		long size = 0;

		// Add file sizes.
		FileInfo[] fis = d.GetFiles();
		foreach (FileInfo fi in fis)
		{
			size += fi.Length;
		}

		// Add subdirectory sizes.
		DirectoryInfo[] dis = d.GetDirectories();
		foreach (DirectoryInfo di in dis)
		{
			size += DirSize(di);
		}
		return size;
	}

	public static string GetSingleUsbDrive()
	{
		IEnumerable<DriveInfo> driveList;
		if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
		{
			driveList = DriveInfo.GetDrives().Where(d => d.Name.Contains("USB")).ToArray();
		}
		else
		{
			driveList = DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Removable);
		}

		return driveList.Single().Name;
	}
}
