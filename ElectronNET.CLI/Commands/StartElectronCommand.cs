﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using ElectronNET.CLI.Commands.Actions;

namespace ElectronNET.CLI.Commands
{
    public class StartElectronCommand : ICommand
    {
        public const string COMMAND_NAME = "start";
        public const string COMMAND_DESCRIPTION = "Start your ASP.NET Core Application with Electron, without package it as a single exe. Faster for development.";
        public const string COMMAND_ARGUMENTS = "<Path> from ASP.NET Core Project.";
        public static IList<CommandOption> CommandOptions { get; set; } = new List<CommandOption>();

        private string[] _args;

        public StartElectronCommand(string[] args)
        {
            _args = args;
        }

        private string _aspCoreProjectPath = "project-path";
        private string _arguments = "args";
        private string _manifest = "manifest";
        private string _clearCache = "clear-cache";

        public Task<bool> ExecuteAsync()
        {
            return Task.Run(() =>
            {
                Console.WriteLine("Start Electron Desktop Application...");

                SimpleCommandLineParser parser = new SimpleCommandLineParser();
                parser.Parse(_args);

                string aspCoreProjectPath = "";

                if (parser.Arguments.ContainsKey(_aspCoreProjectPath))
                {
                    string projectPath = parser.Arguments[_aspCoreProjectPath].First();
                    if (Directory.Exists(projectPath))
                    {
                        aspCoreProjectPath = projectPath;
                    }
                }
                else
                {
                    aspCoreProjectPath = Directory.GetCurrentDirectory();
                }

                string tempPath = Path.Combine(aspCoreProjectPath, "obj", "Host");
                if (Directory.Exists(tempPath) == false)
                {
                    Directory.CreateDirectory(tempPath);
                }

                var platformInfo = GetTargetPlatformInformation.Do(string.Empty, string.Empty);

                string tempBinPath = Path.Combine(tempPath, "bin");
                var resultCode = 0;

                if (parser != null && parser.Contains("watch"))
                {

                    // no need for this code i will remove this before PRS
                    //if (!Directory.Exists($"{tempBinPath}")) Directory.CreateDirectory(tempBinPath);
                    //if (!Directory.Exists($"{tempBinPath}\\wwwroot")) resultCode = ProcessHelper.CmdExecute($"mklink /D {tempBinPath}\\wwwroot wwwroot", aspCoreProjectPath);

                    //if (!File.Exists($"{tempBinPath}\\electron.manifest.json"))
                    //{
                    //    resultCode = ProcessHelper.CmdExecute($"mklink /h {tempBinPath}\\electron.manifest.json electron.manifest.json", aspCoreProjectPath);
                    //}

                }
                else
                {
                    resultCode = ProcessHelper.CmdExecute($"dotnet publish -r {platformInfo.NetCorePublishRid} --output \"{tempBinPath}\" /p:PublishReadyToRun=true --no-self-contained", aspCoreProjectPath);
                }

                if (resultCode != 0)
                {
                    Console.WriteLine("Error occurred during dotnet publish: " + resultCode);
                    return false;
                }

                DeployEmbeddedElectronFiles.Do(tempPath);

                var nodeModulesDirPath = Path.Combine(tempPath, "node_modules");

                Console.WriteLine("node_modules missing in: " + nodeModulesDirPath);

                Console.WriteLine("Start npm install...");
                ProcessHelper.CmdExecute("npm install", tempPath);

                Console.WriteLine("ElectronHostHook handling started...");

                string electronhosthookDir = Path.Combine(Directory.GetCurrentDirectory(), "ElectronHostHook");

                if (Directory.Exists(electronhosthookDir))
                {
                    string hosthookDir = Path.Combine(tempPath, "ElectronHostHook");
                    DirectoryCopy.Do(electronhosthookDir, hosthookDir, true, new List<string>() { "node_modules" });

                    Console.WriteLine("Start npm install for typescript & hosthooks...");
                    ProcessHelper.CmdExecute("npm install", hosthookDir);

                    // ToDo: Not sure if this runs under linux/macos
                    ProcessHelper.CmdExecute(@"npx tsc -p ../../ElectronHostHook", tempPath);
                }

                string arguments = "";

                if (parser.Arguments.ContainsKey(_arguments))
                {
                    arguments = string.Join(' ', parser.Arguments[_arguments]);
                }

                if (parser.Arguments.ContainsKey(_manifest))
                {
                    arguments += " --manifest=" + parser.Arguments[_manifest].First();
                }

                if (parser.Arguments.ContainsKey(_clearCache))
                {
                    arguments += " --clear-cache=true";
                }

                if (parser.Arguments.ContainsKey("watch"))
                {
                    arguments += " --watch=true";
                }

                string path = Path.Combine(tempPath, "node_modules", ".bin");
                bool isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

                if (isWindows)
                {
                    Console.WriteLine("Invoke electron.cmd - in dir: " + path);
                    ProcessHelper.CmdExecute(@"electron.cmd ""..\..\main.js"" " + arguments, path);

                }
                else
                {
                    Console.WriteLine("Invoke electron - in dir: " + path);
                    ProcessHelper.CmdExecute(@"./electron ""../../main.js"" " + arguments, path);
                }

                return true;
            });
        }


    }
}
