using BepInEx.IL2CPP;
using System.Runtime.CompilerServices;

namespace AntiCrasher
{
    internal static class PersistentDataCompatibility
    {
        internal static bool? enabled;
        internal static bool Enabled
            => enabled == null ? (bool)(enabled = IL2CPPChainloader.Instance.Plugins.ContainsKey("lammas123.PersistentData")) : enabled.Value;

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        internal static bool SetClientData(ulong clientId, string key, string value)
        {
            PersistentData.ClientDataFile file = PersistentData.Api.GetClientDataFile(clientId);
            bool valid = file.Set(key, value);
            file.SaveFile();
            return valid;
        }
    }
}