using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace SellAllFloorFix
{
    internal static class Despawner
    {
        public static bool IsServer()
        {
            try
            {
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    // (1) Qualquer NetworkBehaviour -> IsServer / IsServerInitialized.
                    Type? nb = null;
                    try { nb = asm.GetType("FishNet.Object.NetworkBehaviour"); } catch { nb = null; }
                    if (nb != null)
                    {
                        Component? inst = null;
                        try { inst = UnityEngine.Object.FindObjectOfType(nb) as Component; } catch { inst = null; }
                        if (inst != null)
                        {
                            var p1 = nb.GetProperty("IsServer", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                            if (p1 != null && p1.PropertyType == typeof(bool) && p1.CanRead)
                            {
                                try { return (bool)p1.GetValue(inst, null)!; } catch { }
                            }
                            var p2 = nb.GetProperty("IsServerInitialized", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                            if (p2 != null && p2.PropertyType == typeof(bool) && p2.CanRead)
                            {
                                try { return (bool)p2.GetValue(inst, null)!; } catch { }
                            }
                        }
                    }
                    // (1b) MoneyManager também é NetworkBehaviour: checar direto.
                    Type? mm = null;
                    try { mm = asm.GetType("MoneyManager"); } catch { mm = null; }
                    if (mm != null)
                    {
                        Component? minst = null;
                        try { minst = UnityEngine.Object.FindObjectOfType(mm) as Component; } catch { minst = null; }
                        if (minst != null)
                        {
                            var p1 = mm.GetProperty("IsServer", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                            if (p1 != null && p1.PropertyType == typeof(bool) && p1.CanRead)
                            {
                                try { return (bool)p1.GetValue(minst, null)!; } catch { }
                            }
                            var p2 = mm.GetProperty("IsServerInitialized", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                            if (p2 != null && p2.PropertyType == typeof(bool) && p2.CanRead)
                            {
                                try { return (bool)p2.GetValue(minst, null)!; } catch { }
                            }
                        }
                    }
                    // (2) NetworkManager.IsServer / IsServerStarted.
                    Type? nm = null;
                    try { nm = asm.GetType("FishNet.Managing.NetworkManager"); } catch { nm = null; }
                    if (nm != null)
                    {
                        Component? ninst = null;
                        try { ninst = UnityEngine.Object.FindObjectOfType(nm) as Component; } catch { ninst = null; }
                        if (ninst != null)
                        {
                            var p1 = nm.GetProperty("IsServer", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                            if (p1 != null && p1.PropertyType == typeof(bool) && p1.CanRead && p1.GetGetMethod(true) != null)
                            {
                                try { return (bool)p1.GetValue(p1.GetGetMethod(true)!.IsStatic ? null : ninst, null)!; } catch { }
                            }
                            var p2 = nm.GetProperty("IsServerStarted", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                            if (p2 != null && p2.PropertyType == typeof(bool) && p2.CanRead && p2.GetGetMethod(true) != null)
                            {
                                try { return (bool)p2.GetValue(p2.GetGetMethod(true)!.IsStatic ? null : ninst, null)!; } catch { }
                            }
                        }
                    }
                }
            }
            catch (Exception e) { Plugin.Log.LogDebug("IsServer check falhou: " + e.GetBaseException().Message); }
            // (3) Default true (single-player/host por padrão). NUNCA usar InstanceFinder.IsServer (inexistente).
            return true;
        }

        public static IEnumerator DespawnBatch(List<GroundItem> items, int perFrame, Action<int> onDone)
        {
            int done = 0;
            int batch = 0;
            foreach (var gi in items)
            {
                DespawnOne(gi);
                done++;
                batch++;
                if (batch >= Math.Max(1, perFrame))
                {
                    batch = 0;
                    yield return null;
                }
            }
            onDone(done);
        }

        public static void DespawnOne(GroundItem gi)
        {
            try
            {
                if (gi.Go == null) return;
                if (gi.NetObj != null)
                {
                    var t = gi.NetObj.GetType();
                    var m = t.GetMethod("Despawn", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
                    if (m != null) { m.Invoke(gi.NetObj, null); return; }
                    // Fallback: ServerManager.Despawn(go) — lookup tolerante a overloads
                    // (Despawn(GameObject) ou Despawn(GameObject, DespawnType?)) e nunca
                    // pula o Destroy final: exceção aqui cai no Destroy abaixo, não no catch.
                    try
                    {
                        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                        {
                            Type? sm = null;
                            try { sm = asm.GetType("FishNet.Managing.Server.ServerManager"); } catch { continue; }
                            if (sm == null)
                            {
                                try { sm = asm.GetType("FishNet.Object.ServerManager"); } catch { continue; }
                            }
                            if (sm == null) continue;
                            var inst2 = UnityEngine.Object.FindObjectOfType(sm) as Component;
                            if (inst2 == null) continue;
                            MethodInfo? dm = null;
                            try { dm = sm.GetMethod("Despawn", BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(GameObject) }, null); } catch { dm = null; }
                            if (dm == null)
                            {
                                foreach (var cand in sm.GetMethods(BindingFlags.Public | BindingFlags.Instance))
                                {
                                    if (cand.Name != "Despawn") continue;
                                    var cps = cand.GetParameters();
                                    if (cps.Length >= 1 && cps[0].ParameterType == typeof(GameObject)) { dm = cand; break; }
                                }
                            }
                            if (dm == null) continue;
                            var ps = dm.GetParameters();
                            object?[] args = ps.Length == 1 ? new object?[] { gi.Go } : new object?[] { gi.Go, null };
                            try { dm.Invoke(inst2, args); return; }
                            catch (Exception e) { Plugin.Log.LogDebug("ServerManager.Despawn falhou: " + e.GetBaseException().Message); }
                        }
                    }
                    catch (Exception e) { Plugin.Log.LogDebug("Fallback ServerManager falhou: " + e.GetBaseException().Message); }
                }
                UnityEngine.Object.Destroy(gi.Go);
            }
            catch (Exception e) { Plugin.Log.LogDebug("Despawn falhou p/ " + (gi.Go != null ? gi.Go.name : "?") + ": " + e.GetBaseException().Message); }
        }
    }
}
