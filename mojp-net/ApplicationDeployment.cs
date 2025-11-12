using System;

namespace Mojp;

#if NET

public static class ApplicationDeployment
{
    /// <summary>
    /// Gets a value indicating whether the current application is a ClickOnce application.
    /// </summary>
    public static bool IsNetworkDeployed
    {
        get
        {
            _ = bool.TryParse(Environment.GetEnvironmentVariable("ClickOnce_IsNetworkDeployed"), out bool value);
            return value;
        }
    }

    /// <summary>
    /// Gets the URL used to launch the deployment manifest of the application.
    /// </summary>
    public static Uri ActivationUri
    {
        get
        {
            _ = Uri.TryCreate(Environment.GetEnvironmentVariable("ClickOnce_ActivationUri"), UriKind.Absolute, out var uri);
            return uri;
        }
    }

    /// <summary>
    /// Gets the version of the deployment for the current running instance of the application.
    /// </summary>
    public static Version CurrentVersion
    {
        get
        {
            _ = Version.TryParse(Environment.GetEnvironmentVariable("ClickOnce_CurrentVersion"), out var version);
            return version;
        }
    }

    /// <summary>
    /// Gets the path to the ClickOnce data directory.
    /// </summary>
    public static string DataDirectory => Environment.GetEnvironmentVariable("ClickOnce_DataDirectory");

    /// <summary>
    /// Gets a value indicating whether this is the first time this application has run on the client computer.
    /// </summary>
    public static bool IsFirstRun
    {
        get
        {
            _ = bool.TryParse(Environment.GetEnvironmentVariable("ClickOnce_IsFirstRun"), out bool version);
            return version;
        }
    }

    /// <summary>
    /// Gets the date and the time ClickOnce last checked for an application update.
    /// </summary>
    public static DateTime TimeOfLastUpdateCheck
    {
        get
        {
            _ = DateTime.TryParse(Environment.GetEnvironmentVariable("ClickOnce_TimeOfLastUpdateCheck"), out var version);
            return version;
        }
    }

    /// <summary>
    /// Gets the full name of the application after it has been updated.
    /// </summary>
    public static string UpdatedApplicationFullName => Environment.GetEnvironmentVariable("ClickOnce_UpdatedApplicationFullName");

    /// <summary>
    /// Gets the version of the update that was recently downloaded.
    /// </summary>
    public static Version UpdatedVersion
    {
        get
        {
            _ = Version.TryParse(Environment.GetEnvironmentVariable("ClickOnce_UpdatedVersion"), out var version);
            return version;
        }
    }

    /// <summary>
    /// Gets the Web site or file share from which this application updates itself.
    /// </summary>
    public static Uri UpdateLocation
    {
        get
        {
            _ = Uri.TryCreate(Environment.GetEnvironmentVariable("ClickOnce_UpdateLocation"), UriKind.Absolute, out var uri);
            return uri;
        }
    }

    public static Version LauncherVersion
    {
        get
        {

            _ = Version.TryParse(Environment.GetEnvironmentVariable("ClickOnce_LauncherVersion"), out var version);
            return version;
        }
    }
}

#endif
