using System.Text.RegularExpressions;
using NAPS2.Tools.Project.Targets;

namespace NAPS2.Tools.Project.Packaging;

public class PackageCommand : ICommand<PackageOptions>
{
    public int Run(PackageOptions opts)
    {
        if (opts.Build)
        {
            new BuildCommand().Run(new BuildOptions
            {
                BuildType = opts.BuildType
            });
        }

        // TODO: Fix windows targets to ensure that the project is built
        // TODO: Allow customizing dotnet version
        var constraints = new TargetConstraints
        {
            AllowMultiplePlatforms = true,
            RequireBuildablePlatform = true
        };
        foreach (var target in TargetsHelper.Enumerate(opts.BuildType, opts.Platform, constraints))
        {
            PackageInfo GetPackageInfoForConfig(string config) => GetPackageInfo(target.Platform, config, opts.Name);
            switch (target.BuildType)
            {
                case BuildType.Exe:
                    // TODO: We might need configs designed for mac + linux
                    if (target.Platform.IsLinux())
                    {
                        FlatpakPackager.Package(GetPackageInfoForConfig("Release-Linux"), opts.NoPre);
                    }
                    else if (target.Platform.IsMac())
                    {
                        MacPackager.Package(GetPackageInfoForConfig("Release"), opts.NoSign, opts.NoNotarize);
                    }
                    else if (target.Platform.IsWindows())
                    {
                        InnoSetupPackager.PackageExe(GetPackageInfoForConfig("Release"));
                    }
                    break;
                case BuildType.Msi:
                    WixToolsetPackager.PackageMsi(GetPackageInfoForConfig("Release-Msi"));
                    break;
                case BuildType.Zip:
                    ZipArchivePackager.PackageZip(GetPackageInfoForConfig("Release-Zip"));
                    break;
            }
        }
        return 0;
    }

    private static PackageInfo GetPackageInfo(Platform platform, string preferredConfig, string? packageName)
    {
        var pkgInfo = new PackageInfo(platform, ProjectHelper.GetCurrentVersionName(),
            ProjectHelper.GetCurrentVersion(), packageName);

        if (!platform.IsWindows())
        {
            // We rely on "dotnet publish" to build the installer
            return pkgInfo;
        }

        foreach (var project in new[]
                     { "NAPS2.Sdk", "NAPS2.Lib", "NAPS2.App.Worker", "NAPS2.App.WinForms", "NAPS2.App.Console" })
        {
            var buildPath = Path.Combine(Paths.SolutionRoot, project, "bin", preferredConfig, "net462");
            if (!Directory.Exists(buildPath))
            {
                buildPath = Path.Combine(Paths.SolutionRoot, project, "bin", "Release", "net462");
            }
            if (!Directory.Exists(buildPath))
            {
                throw new Exception($"Could not find build path. Maybe run 'n2 build' first? {buildPath}");
            }
            PopulatePackageInfo(buildPath, platform, pkgInfo);
        }

        var appBuildPath = Path.Combine(Paths.SolutionRoot, "NAPS2.App.WinForms", "bin", "Release", "net462");
        if (platform == Platform.Win)
        {
            AddPlatformFiles(pkgInfo, appBuildPath, "_win32");
            AddPlatformFiles(pkgInfo, appBuildPath, "_win64");
        }
        else if (platform == Platform.Win32)
        {
            AddPlatformFiles(pkgInfo, appBuildPath, "_win32");
            // Even for the 32-bit package, we can always install it on a 64-bit machine. That's a problem as .NET
            // AnyCPU will run in 64-bit mode and we won't be able to load the 32-bit DLLs, so we have to include 64-bit
            // too.
            // TODO: Any better way to handle this? Run everything in the guaranteed 32-bit worker? Have a separate WinForms32 project?
            // Or maybe we can deprecate the 32-bit MSI installer. Idk.
            AddPlatformFile(pkgInfo, appBuildPath, "_win64", "NAPS2.Wia.Native.dll");
            AddPlatformFile(pkgInfo, appBuildPath, "_win64", "twaindsm.dll");
            AddPlatformFile(pkgInfo, appBuildPath, "_win64", "pdfium.dll");
        }
        else if (platform == Platform.Win64)
        {
            AddPlatformFiles(pkgInfo, appBuildPath, "_win64");
            // Special case as we have a 64 bit main app and a 32 bit worker
            AddPlatformFile(pkgInfo, appBuildPath, "_win32", "NAPS2.Wia.Native.dll");
            AddPlatformFile(pkgInfo, appBuildPath, "_win32", "twaindsm.dll");
            // TODO: We should run pdfium in a 64-bit worker
            AddPlatformFile(pkgInfo, appBuildPath, "_win32", "pdfium.dll");
        }
        else
        {
            throw new Exception("Unsupported platform");
        }
        pkgInfo.AddFile(new PackageFile(appBuildPath, "", "appsettings.xml"));
        pkgInfo.AddFile(new PackageFile(Paths.SolutionRoot, "", "LICENSE", "license.txt"));
        pkgInfo.AddFile(new PackageFile(Paths.SolutionRoot, "", "CONTRIBUTORS", "contributors.txt"));
        return pkgInfo;
    }

    private static void PopulatePackageInfo(string buildPath, Platform platform, PackageInfo pkgInfo)
    {
        var dir = new DirectoryInfo(buildPath);
        if (!dir.Exists)
        {
            throw new Exception($"Could not find path: {dir.FullName}");
        }
        foreach (var exeFile in dir.EnumerateFiles("*.exe"))
        {
            var dest = exeFile.Name.ToLower() switch
            {
                "naps2.worker.exe" => "lib",
                _ => ""
            };
            pkgInfo.AddFile(exeFile, dest);
        }
        foreach (var configFile in dir.EnumerateFiles("*.exe.config"))
        {
            var dest = configFile.Name.ToLower() switch
            {
                "naps2.worker.exe.config" => "lib",
                _ => ""
            };
            pkgInfo.AddFile(configFile, dest);
        }
        foreach (var dllFile in dir.EnumerateFiles("*.dll"))
        {
            // TODO: Blacklist unneeded dlls
            pkgInfo.AddFile(dllFile, "lib");
        }
        foreach (var langFolder in dir.EnumerateDirectories()
                     .Where(x => Regex.IsMatch(x.Name, "[a-z]{2}(-[A-Za-z]+)?")))
        {
            foreach (var resourceDll in langFolder.EnumerateFiles("*.resources.dll"))
            {
                pkgInfo.AddFile(resourceDll, Path.Combine("lib", langFolder.Name));
                pkgInfo.Languages.Add(langFolder.Name);
            }
        }
    }

    private static void AddPlatformFiles(PackageInfo pkgInfo, string buildPath, string platformPath)
    {
        var folder = new DirectoryInfo(Path.Combine(buildPath, platformPath));
        foreach (var file in folder.EnumerateFiles())
        {
            pkgInfo.AddFile(new PackageFile(file.DirectoryName ?? "", Path.Combine("lib", platformPath), file.Name));
        }
    }

    private static void AddPlatformFile(PackageInfo pkgInfo, string buildPath, string platformPath, string fileName)
    {
        pkgInfo.AddFile(new PackageFile(Path.Combine(buildPath, platformPath), Path.Combine("lib", platformPath),
            fileName));
    }
}