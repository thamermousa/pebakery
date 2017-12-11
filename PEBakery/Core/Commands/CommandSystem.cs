﻿/*
    Copyright (C) 2016-2017 Hajin Jang
    Licensed under GPL 3.0
 
    PEBakery is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.
*/

using PEBakery.Exceptions;
using PEBakery.Helper;
using PEBakery.WPF;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace PEBakery.Core.Commands
{
    public static class CommandSystem
    {
        public static List<LogInfo> SystemCmd(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_System));
            CodeInfo_System info = cmd.Info as CodeInfo_System;

            SystemType type = info.Type;
            switch (type)
            {
                case SystemType.Cursor:
                    {
                        Debug.Assert(info.SubInfo.GetType() == typeof(SystemInfo_Cursor));
                        SystemInfo_Cursor subInfo = info.SubInfo as SystemInfo_Cursor;

                        string iconStr = StringEscaper.Preprocess(s, subInfo.IconKind);

                        if (iconStr.Equals("WAIT", StringComparison.OrdinalIgnoreCase))
                        {
                            System.Windows.Forms.Cursor.Current = System.Windows.Forms.Cursors.WaitCursor;
                            logs.Add(new LogInfo(LogState.Success, "Mouse cursor icon set to [Wait]"));
                        }
                        else if (iconStr.Equals("NORMAL", StringComparison.OrdinalIgnoreCase))
                        {
                            System.Windows.Forms.Cursor.Current = System.Windows.Forms.Cursors.Default;
                            logs.Add(new LogInfo(LogState.Success, "Mouse cursor icon set to [Normal]"));
                        }
                        else
                        {
                            logs.Add(new LogInfo(LogState.Error, $"Wrong mouse cursor icon [{iconStr}]"));
                        }
                    }
                    break;
                case SystemType.ErrorOff:
                    {
                        Debug.Assert(info.SubInfo.GetType() == typeof(SystemInfo_ErrorOff));
                        SystemInfo_ErrorOff subInfo = info.SubInfo as SystemInfo_ErrorOff;

                        string linesStr = StringEscaper.Preprocess(s, subInfo.Lines);
                        if (!NumberHelper.ParseInt32(linesStr, out int lines))
                            throw new ExecuteException($"[{linesStr}] is not a valid integer");
                        if (lines <= 0)
                            throw new ExecuteException($"[{linesStr}] must be positive integer");

                        // ExecuteCommand decrease ErrorOffCount after executing one command.
                        s.Logger.ErrorOffCount = lines + 1; // So add 1

                        logs.Add(new LogInfo(LogState.Success, $"Error is off for [{lines}] lines"));
                    }
                    break;
                case SystemType.GetEnv:
                    {
                        Debug.Assert(info.SubInfo.GetType() == typeof(SystemInfo_GetEnv));
                        SystemInfo_GetEnv subInfo = info.SubInfo as SystemInfo_GetEnv;

                        string envVarName = StringEscaper.Preprocess(s, subInfo.EnvVarName);
                        string envVarValue = Environment.GetEnvironmentVariable(envVarName);
                        if (envVarValue == null) // Failure
                        {
                            logs.Add(new LogInfo(LogState.Ignore, $"Cannot get environment variable [%{envVarName}%]'s value"));
                            envVarValue = string.Empty;
                        }

                        logs.Add(new LogInfo(LogState.Success, $"Environment variable [{envVarName}]'s value is [{envVarValue}]"));
                        List<LogInfo> varLogs = Variables.SetVariable(s, subInfo.DestVar, envVarValue);
                        logs.AddRange(varLogs);
                    }
                    break;
                case SystemType.GetFreeDrive:
                    {
                        Debug.Assert(info.SubInfo.GetType() == typeof(SystemInfo_GetFreeDrive));
                        SystemInfo_GetFreeDrive subInfo = info.SubInfo as SystemInfo_GetFreeDrive;

                        DriveInfo[] drives = DriveInfo.GetDrives();
                        string letters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
                        char lastFreeLetter = letters.Except(drives.Select(d => d.Name[0])).LastOrDefault();

                        if (lastFreeLetter != '\0') // Success
                        {
                            logs.Add(new LogInfo(LogState.Success, $"Last free drive letter is [{lastFreeLetter}]"));
                            List<LogInfo> varLogs = Variables.SetVariable(s, subInfo.DestVar, lastFreeLetter.ToString());
                            logs.AddRange(varLogs);
                        }
                        else // No Free Drives
                        {
                            // TODO: Is it correct WB082 behavior?
                            logs.Add(new LogInfo(LogState.Ignore, "No free drive letter")); 
                            List<LogInfo> varLogs = Variables.SetVariable(s, subInfo.DestVar, string.Empty);
                            logs.AddRange(varLogs);
                        }
                    }
                    break;
                case SystemType.GetFreeSpace:
                    {
                        Debug.Assert(info.SubInfo.GetType() == typeof(SystemInfo_GetFreeSpace));
                        SystemInfo_GetFreeSpace subInfo = info.SubInfo as SystemInfo_GetFreeSpace;

                        string path = StringEscaper.Preprocess(s, subInfo.Path);

                        FileInfo f = new FileInfo(path);
                        DriveInfo drive = new DriveInfo(f.Directory.Root.FullName);
                        long freeSpaceMB = drive.TotalFreeSpace / (1024 * 1024); // B to MB

                        List<LogInfo> varLogs = Variables.SetVariable(s, subInfo.DestVar, freeSpaceMB.ToString());
                        logs.AddRange(varLogs);
                    }
                    break;
                case SystemType.IsAdmin:
                    {
                        Debug.Assert(info.SubInfo.GetType() == typeof(SystemInfo_IsAdmin));
                        SystemInfo_IsAdmin subInfo = info.SubInfo as SystemInfo_IsAdmin;

                        bool isAdmin;
                        using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
                        {
                            WindowsPrincipal principal = new WindowsPrincipal(identity);
                            isAdmin = principal.IsInRole(WindowsBuiltInRole.Administrator);
                        }

                        if (isAdmin)
                            logs.Add(new LogInfo(LogState.Success, "PEBakery is running as Administrator"));
                        else
                            logs.Add(new LogInfo(LogState.Success, "PEBakery is not running as Administrator"));

                        List<LogInfo> varLogs = Variables.SetVariable(s, subInfo.DestVar, isAdmin.ToString());
                        logs.AddRange(varLogs);
                    }
                    break;
                case SystemType.OnBuildExit:
                    {
                        Debug.Assert(info.SubInfo.GetType() == typeof(SystemInfo_OnBuildExit));
                        SystemInfo_OnBuildExit subInfo = info.SubInfo as SystemInfo_OnBuildExit;

                        s.OnBuildExit = subInfo.Cmd;

                        logs.Add(new LogInfo(LogState.Success, "OnBuildExit event registered"));
                    }
                    break;
                case SystemType.OnScriptExit:
                case SystemType.OnPluginExit:
                    {
                        Debug.Assert(info.SubInfo.GetType() == typeof(SystemInfo_OnPluginExit));
                        SystemInfo_OnPluginExit subInfo = info.SubInfo as SystemInfo_OnPluginExit;

                        s.OnPluginExit = subInfo.Cmd;

                        logs.Add(new LogInfo(LogState.Success, "OnPluginExit event registered"));
                    }
                    break;
                case SystemType.RefreshInterface:
                    {
                        Debug.Assert(info.SubInfo.GetType() == typeof(SystemInfo_RefreshInterface));
                        SystemInfo_RefreshInterface subInfo = info.SubInfo as SystemInfo_RefreshInterface;

                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            MainWindow w = (Application.Current.MainWindow as MainWindow);

                            if (w.CurMainTree.Plugin.Equals(cmd.Addr.Plugin))
                                w.StartReloadPluginWorker();
                        });

                        logs.Add(new LogInfo(LogState.Success, $"Rerendered plugin [{cmd.Addr.Plugin.Title}]"));
                    }
                    break;
                case SystemType.RescanScripts:
                    {
                        Debug.Assert(info.SubInfo.GetType() == typeof(SystemInfo_RescanScripts));
                        SystemInfo_RescanScripts subInfo = info.SubInfo as SystemInfo_RescanScripts;

                        // Reload Project
                        BackgroundWorker worker = null;
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            MainWindow w = (Application.Current.MainWindow as MainWindow);
                            worker = w.StartLoadWorker(true);                
                        });
                        
                        // TODO: More elegant way?
                        Task.Run(() =>
                        {
                            while (worker.IsBusy)
                                Thread.Sleep(200);
                        }).Wait();

                        logs.Add(new LogInfo(LogState.Success, $"Reload project [{cmd.Addr.Plugin.Project.ProjectName}]"));
                    }
                    break;
                case SystemType.Rescan:
                    {
                        Debug.Assert(info.SubInfo.GetType() == typeof(SystemInfo_Rescan));
                        SystemInfo_Rescan subInfo = info.SubInfo as SystemInfo_Rescan;

                        string pPath = StringEscaper.Preprocess(s, subInfo.PluginToRefresh);
                        string pFullPath = Path.GetFullPath(pPath);

                        // Reload plugin
                        Plugin p = Engine.GetPluginInstance(s, cmd, cmd.Addr.Plugin.FullPath, pFullPath, out bool inCurrentPlugin);
                        p = s.Project.RefreshPlugin(p, s);
                        if (p == null)
                        {
                            logs.Add(new LogInfo(LogState.Error, $"Reloading plugin [{pFullPath}] failed"));
                            return logs;
                        }

                        // Update MainWindow
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            MainWindow w = (Application.Current.MainWindow as MainWindow);
                            if (p.Equals(w.CurMainTree.Plugin))
                            {
                                w.CurMainTree.Plugin = p;
                                w.DrawPlugin(w.CurMainTree.Plugin);
                            }
                        });

                        logs.Add(new LogInfo(LogState.Success, $"Reload project [{cmd.Addr.Plugin.Project.ProjectName}]"));
                    }
                    break;
                case SystemType.SaveLog:
                    {
                        Debug.Assert(info.SubInfo.GetType() == typeof(SystemInfo_SaveLog));
                        SystemInfo_SaveLog subInfo = info.SubInfo as SystemInfo_SaveLog;

                        string destPath = StringEscaper.Preprocess(s, subInfo.DestPath);
                        string logFormatStr = StringEscaper.Preprocess(s, subInfo.LogFormat);

                        LogExportType logFormat = Logger.ParseLogExportType(logFormatStr);

                        s.Logger.Build_Write(s, new LogInfo(LogState.Success, $"Exported Build Logs to [{destPath}]", cmd, s.CurDepth));
                        s.Logger.ExportBuildLog(logFormat, destPath, s.BuildId);
                    }
                    break;
                    // WB082 Compability Shim
                case SystemType.HasUAC:
                    {
                        Debug.Assert(info.SubInfo.GetType() == typeof(SystemInfo_HasUAC));
                        SystemInfo_HasUAC subInfo = info.SubInfo as SystemInfo_HasUAC;

                        logs.Add(new LogInfo(LogState.Warning, $"[System,HasUAC] is deprecated"));

                        // Deprecated, WB082 Compability Shim
                        List<LogInfo> varLogs = Variables.SetVariable(s, subInfo.DestVar, "True");
                        logs.AddRange(varLogs);
                    }
                    break;
                case SystemType.FileRedirect: // Do nothing
                    logs.Add(new LogInfo(LogState.Ignore, $"[System,FileRedirect] is not necessary in PEBakery"));
                    break;
                case SystemType.RegRedirect: // Do nothing
                    logs.Add(new LogInfo(LogState.Ignore, $"[System,RegRedirect] is not necessary in PEBakery"));
                    break;
                case SystemType.RebuildVars: 
                    { // Reset Variables to clean state
                        s.Variables.ResetVariables(VarsType.Fixed);
                        s.Variables.ResetVariables(VarsType.Global);
                        s.Variables.ResetVariables(VarsType.Local);

                        // Load Global Variables
                        List<LogInfo> varLogs;
                        varLogs = s.Variables.LoadDefaultGlobalVariables();
                        logs.AddRange(LogInfo.AddDepth(varLogs, s.CurDepth + 1));

                        // Load Per-Plugin Variables
                        varLogs = s.Variables.LoadDefaultPluginVariables(cmd.Addr.Plugin);
                        logs.AddRange(LogInfo.AddDepth(varLogs, s.CurDepth + 1));

                        // Load Per-Plugin Macro
                        s.Macro.ResetLocalMacros();
                        varLogs = s.Macro.LoadLocalMacroDict(cmd.Addr.Plugin);
                        logs.AddRange(LogInfo.AddDepth(varLogs, s.CurDepth + 1));

                        logs.Add(new LogInfo(LogState.Success, $"Variables are reset to default state"));
                    }
                    break;
                default: // Error
                    throw new InvalidCodeCommandException($"Wrong SystemType [{type}]");
            }

            return logs;
        }

        /// <summary>
        /// Function for ShellExecute, ShellExecuteDelete
        /// </summary>
        /// <param name="s"></param>
        /// <param name="cmd"></param>
        /// <returns></returns>
        public static List<LogInfo> ShellExecute(EngineState s, CodeCommand cmd)
        {
            List<LogInfo> logs = new List<LogInfo>();

            Debug.Assert(cmd.Info.GetType() == typeof(CodeInfo_ShellExecute));
            CodeInfo_ShellExecute info = cmd.Info as CodeInfo_ShellExecute;

            string verb = StringEscaper.Preprocess(s, info.Action);
            string filePath = StringEscaper.Preprocess(s, info.FilePath);

            // Must not check existance of filePath with File.Exists()!
            // Because of PATH envrionment variable, it prevents call of system executables.
            // Ex) cmd.exe does not exist in %BaseDir%, but in System32 directory.

            StringBuilder b = new StringBuilder(filePath);
            using (Process proc = new Process())
            {
                proc.StartInfo.FileName = filePath;
                if (info.Params != null && info.Params.Equals(string.Empty, StringComparison.Ordinal) == false)
                {
                    string parameters = StringEscaper.Preprocess(s, info.Params);
                    proc.StartInfo.Arguments = parameters;
                    b.Append(" ");
                    b.Append(parameters);
                }

                string pathVarBackup = null;
                if (info.WorkDir != null)
                {
                    string workDir = StringEscaper.Preprocess(s, info.WorkDir);
                    proc.StartInfo.WorkingDirectory = workDir;

                    // Set PATH environment variable (only for this process)
                    pathVarBackup = Environment.GetEnvironmentVariable("PATH");
                    Environment.SetEnvironmentVariable("PATH", workDir + ";" + pathVarBackup);
                }

                try
                {
                    if (verb.Equals("Open", StringComparison.OrdinalIgnoreCase))
                    {
                        proc.StartInfo.UseShellExecute = true;
                        proc.StartInfo.Verb = "Open";
                    }
                    else if (verb.Equals("Hide", StringComparison.OrdinalIgnoreCase))
                    {
                        proc.StartInfo.UseShellExecute = false;
                        proc.StartInfo.Verb = "Open";
                        proc.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                        proc.StartInfo.CreateNoWindow = true;

                        // Redirecting standard stream without reading can full buffer, which leads to hang
                        //proc.StartInfo.RedirectStandardError = true;
                        //proc.StartInfo.RedirectStandardOutput = true;
                    }
                    else
                    {
                        proc.StartInfo.Verb = verb;
                    }

                    // Register process instance in EngineState, and run it
                    s.RunningSubProcess = proc;
                    proc.Start();
                    proc.Exited += (object sender, EventArgs e) => {
                        s.RunningSubProcess = null;
                    };

                    switch (cmd.Type)
                    {
                        case CodeType.ShellExecute:
                            proc.WaitForExit();
                            logs.Add(new LogInfo(LogState.Success, $"Executed [{b}], returned exit code [{proc.ExitCode}]"));
                            break;
                        case CodeType.ShellExecuteEx:
                            logs.Add(new LogInfo(LogState.Success, $"Executed [{b}]"));
                            break;
                        case CodeType.ShellExecuteDelete:
                            proc.WaitForExit();
                            File.Delete(filePath);
                            logs.Add(new LogInfo(LogState.Success, $"Executed and deleted [{b}], returned exit code [{proc.ExitCode}]"));
                            break;
                        default:
                            throw new InternalException($"Internal Error! Invalid CodeType [{cmd.Type}]. Please report to issue tracker.");
                    }

                    if (cmd.Type != CodeType.ShellExecuteEx)
                    {
                        string exitOutVar;
                        if (info.ExitOutVar == null)
                            exitOutVar = "%ExitCode%"; // WB082 behavior -> even if info.ExitOutVar is not specified, it will save value to %ExitCode%
                        else
                            exitOutVar = info.ExitOutVar;

                        LogInfo log = Variables.SetVariable(s, exitOutVar, proc.ExitCode.ToString()).First();

                        if (log.State == LogState.Success)
                            logs.Add(new LogInfo(LogState.Success, $"Exit code [{proc.ExitCode}] saved into variable [{exitOutVar}]"));
                        else if (log.State == LogState.Error)
                            logs.Add(log);
                        else
                            throw new InternalException($"Internal Error! Invalid LogType [{log.State}]. Please report to issue tracker.");
                    }
                }
                finally
                {
                    // Restore PATH environment variable
                    if (pathVarBackup != null)
                        Environment.SetEnvironmentVariable("PATH", pathVarBackup);
                }
            }

            return logs;
        }
    }
}
