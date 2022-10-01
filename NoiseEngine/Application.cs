﻿using NoiseEngine.Collections.Concurrent;
using NoiseEngine.Jobs;
using NoiseEngine.Logging;
using NoiseEngine.Rendering;
using NoiseEngine.Rendering.Presentation;
using NoiseEngine.Threading;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NoiseEngine.Interop.Logging;

namespace NoiseEngine;

public static class Application {

    private static readonly object exitLocker = new object();
    private static readonly ConcurrentList<ApplicationScene> loadedScenes = new ConcurrentList<ApplicationScene>();

    private static AtomicBool isInitialized;
    private static bool isExited;
    private static ApplicationSettings? settings;

    public static string Name => Settings.Name!;
    public static EntitySchedule EntitySchedule => Settings.EntitySchedule!;

    public static IEnumerable<ApplicationScene> LoadedScenes => loadedScenes;
    public static IEnumerable<Window> Windows => LoadedScenes.SelectMany(x => x.Cameras).Select(x => x.RenderTarget);

    internal static ApplicationSettings Settings => settings ?? throw new InvalidOperationException(
        $"{nameof(Application)} has not been initialized with a call to {nameof(Initialize)}.");

    /// <summary>
    /// Exit handler.
    /// </summary>
    /// <param name="exitCode">The exit code to return to the operating system.</param>
    public delegate void ApplicationExitHandler(int exitCode);

    /// <summary>
    /// This event is executed when <see cref="Exit(int)"/> is called.
    /// </summary>
    public static event ApplicationExitHandler? ApplicationExit;

    /// <summary>
    /// Initializes <see cref="Application"/>.
    /// </summary>
    /// <param name="settings">Application settings.</param>
    /// <exception cref="InvalidOperationException"><see cref="Application"/> has been already initialized.</exception>
    public static void Initialize(ApplicationSettings settings) {
        if (isInitialized.Exchange(true))
            throw new InvalidOperationException($"{nameof(Application)} has been already initialized.");

        AppDomain.CurrentDomain.ProcessExit += CurrentDomainOnExit;

        if (settings.AddDefaultLoggerSinks) {
            if (!Log.Logger.Sinks.Any(x => typeof(ConsoleLogSink) == x.GetType()))
                Log.Logger.AddSink(new ConsoleLogSink(new ConsoleLogSinkSettings { ThreadNameLength = 20 }));
            if (!Log.Logger.Sinks.Any(x => typeof(FileLogSink) == x.GetType()))
                Log.Logger.AddSink(FileLogSink.CreateFromDirectory("logs"));
        }

        InteropLogging.Initialize(Log.Logger);

        // Set default values.
        if (settings.Name is null) {
            Assembly? entryAssembly = Assembly.GetEntryAssembly();

            if (entryAssembly is null)
                settings = settings with { Name = "Unknown" };
            else
                settings = settings with { Name = entryAssembly.GetName().Name ?? entryAssembly.Location };
        }

        Application.settings = settings with {
            EntitySchedule = settings.EntitySchedule ?? new EntitySchedule()
        };
    }

    /// <summary>
    /// Disposes <see cref="Application"/> resources and when ProcessExitOnApplicationExit
    /// setting is <see langword="true"/> ends process with given <paramref name="exitCode"/>.
    /// </summary>
    /// <param name="exitCode">
    /// The exit code to return to the operating system. Use 0 (zero)
    /// to indicate that the process completed successfully.
    /// </param>
    public static void Exit(int exitCode = 0) {
        lock (exitLocker) {
            if (isExited)
                return;
            isExited = true;

            ApplicationExit?.Invoke(exitCode);

            foreach (ApplicationScene scene in LoadedScenes)
                scene.Dispose();

            EntitySchedule.Dispose();
            Graphics.Terminate();

            Log.Info($"{nameof(Application)} exited with code {exitCode}.");
            InteropLogging.Terminate();
            Log.Logger.Dispose();

            AppDomain.CurrentDomain.ProcessExit -= CurrentDomainOnExit;
            if (Settings.ProcessExitOnApplicationExit)
                Environment.Exit(exitCode);
        }
    }

    internal static void AddSceneToLoaded(ApplicationScene scene) {
        loadedScenes.Add(scene);
    }

    internal static void RemoveSceneFromLoaded(ApplicationScene scene) {
        loadedScenes.Remove(scene);
    }

    private static void CurrentDomainOnExit(object? sender, EventArgs e) {
        string info = $"The process was closed without calling {nameof(Application)}.{nameof(Exit)} method.";

        Log.Fatal(info);
        Log.Logger.Dispose();

        throw new ApplicationException(info);
    }

}
