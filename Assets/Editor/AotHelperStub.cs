// Stub to fix missing AotHelper in com.unity.services.core@3464cb68d709
// This package has a bug where JsonHelpers.cs references AotHelper which doesn't exist.
#if UNITY_EDITOR
namespace Unity.Services.Core.Environments.Client.Http
{
    internal static class AotHelper
    {
        public static void EnsureType<T>() { }
    }
}
#endif
