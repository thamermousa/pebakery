﻿using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PEBakery.Core;
using PEBakery.Tests.Core;

namespace PEBakery.Tests
{
    [TestClass]
    public class TestSetup
    {
        #region AssemblyInitalize, AssemblyCleanup
        [AssemblyInitialize]
        public static void PrepareTests(TestContext ctx)
        {
            EngineTests.BaseDir = Path.GetFullPath(Path.Combine("..", "..", "Samples"));
            ProjectCollection projects = new ProjectCollection(EngineTests.BaseDir, null);
            projects.PrepareLoad(out _);
            projects.Load(null);

            // Should be only one project named TestSuite
            EngineTests.Project = projects.Projects[0];

            // Init NativeAssembly
            NativeAssemblyInit();

            // Use InMemory Database for Tests
            Logger.DebugLevel = DebugLevel.PrintExceptionStackTrace;
            EngineTests.Logger = new Logger(":memory:");
            EngineTests.Logger.SystemWrite(new LogInfo(LogState.Info, "PEBakery.Tests launched"));

            App.Logger = EngineTests.Logger;
            App.BaseDir = EngineTests.BaseDir;
        }

        private static void NativeAssemblyInit()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string arch = IntPtr.Size == 8 ? "x64" : "x86";

            string zLibDllPath = Path.Combine(baseDir, arch, "zlibwapi.dll");
            string wimLibDllPath = Path.Combine(baseDir, arch, "libwim-15.dll");
            string xzDllPath = Path.Combine(baseDir, arch, "liblzma.dll");
            string lz4DllPath = Path.Combine(baseDir, arch, "liblz4.so.1.8.1.dll");

            Joveler.ZLibWrapper.ZLibNative.AssemblyInit(zLibDllPath);
            ManagedWimLib.Wim.GlobalInit(wimLibDllPath);
            PEBakery.XZLib.XZStream.GlobalInit(xzDllPath);
            PEBakery.LZ4Lib.LZ4FrameStream.GlobalInit(lz4DllPath);
        }

        [AssemblyCleanup]
        public static void AssemblyCleanup()
        {
            EngineTests.Logger.DB.Close();

            Joveler.ZLibWrapper.ZLibNative.AssemblyCleanup();
            ManagedWimLib.Wim.GlobalCleanup();
            PEBakery.XZLib.XZStream.GlobalCleanup();
            PEBakery.LZ4Lib.LZ4FrameStream.GlobalCleanup();
        }
        #endregion
    }
}
