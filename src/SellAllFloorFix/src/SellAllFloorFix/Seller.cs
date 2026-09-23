using System;
using System.Reflection;
using UnityEngine;

namespace SellAllFloorFix
{
    internal static class Seller
    {
        private static readonly string[] SellMethodNames = { "Sell", "SellItem", "CmdSell", "ServerSell", "SellToShop" };
        private static readonly string[] CreditMethodNames = { "AddMoney", "Add", "GiveMoney", "AddCoins" };
        private static readonly string[] LocalPlayerFlagNames = { "IsLocalPlayer", "isLocalPlayer", "IsLocal", "isLocal", "IsOwner", "isOwner", "IsMine", "isMine", "IsMainPlayer", "isMainPlayer", "IsClient", "isClient" };

        public static bool TryNativeSell(Component item)
        {
            // Tentativa 1 (preferida, real): MoneyManager.SellItem(item) static via reflexão.
            // Rota server-gated que credita TotalWorth + toca som (ver inspection-notes.md).
            try
            {
                var itemType = item.GetType();
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    Type? mmType = null;
                    try { mmType = asm.GetType("MoneyManager"); } catch { continue; }
                    if (mmType == null) continue;
                    foreach (var m in mmType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
                    {
                        if (m.Name != "SellItem") continue;
                        var ps = m.GetParameters();
                        if (ps.Length != 1) continue;
                        if (!ps[0].ParameterType.IsAssignableFrom(itemType)) continue;
                        try { m.Invoke(null, new object[] { item }); return true; }
                        catch (Exception e) { Plugin.Log.LogDebug("Sell nativo MoneyManager.SellItem falhou: " + e.GetBaseException().Message); }
                    }
                }
            }
            catch (Exception e) { Plugin.Log.LogDebug("Sell nativo MoneyManager.SellItem falhou: " + e.GetBaseException().Message); }

            // Tentativa 2 (fallback legado): métodos zero-arg no próprio item.
            var t = item.GetType();
            foreach (var n in SellMethodNames)
            {
                var m = t.GetMethod(n, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, Type.EmptyTypes, null);
                if (m == null) continue;
                try { m.Invoke(item, null); return true; }
                catch (Exception e) { Plugin.Log.LogDebug("Sell nativo " + n + " falhou: " + e.GetBaseException().Message); }
            }
            return false;
        }

        // Retorna true se o crédito foi aplicado; false veta o despawn no chamador (spec §6).
        public static bool Credit(int total)
        {
            if (total <= 0) return true;
            if (TryCreditViaMoneyManager(total)) return true;
            Plugin.Log.LogWarning("SellAll: dinheiro NÃO creditado ($" + total + "); itens mantidos no chão.");
            return false;
        }

        private static bool TryCreditViaMoneyManager(int total)
        {
            Type? playerType = FindPlayerType();
            object? localPlayer = playerType != null ? ResolveLocalPlayer(playerType) : null;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type? mmType = null;
                try { mmType = asm.GetType("MoneyManager"); } catch { continue; }
                if (mmType == null) continue;
                // 1) Rota real: AddMoney(int, Player) estático (ver inspection-notes.md).
                if (playerType != null && localPlayer != null)
                {
                    foreach (var m in mmType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
                    {
                        if (m.Name != "AddMoney") continue;
                        var ps = m.GetParameters();
                        if (ps.Length != 2) continue;
                        if (ps[0].ParameterType != typeof(int)) continue;
                        if (!ps[1].ParameterType.IsAssignableFrom(playerType)) continue;
                        try { m.Invoke(null, new object[] { total, localPlayer }); AfterCreditFx(total); return true; }
                        catch (Exception e) { Plugin.Log.LogDebug("AddMoney(int,Player) estático falhou: " + e.GetBaseException().Message); }
                    }
                    // 2) Rota real via instância singleton: Instance.AddMoney(total, player).
                    try
                    {
                        object? inst = GetMoneyManagerInstance(mmType);
                        if (inst != null)
                        {
                            foreach (var m in mmType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                            {
                                if (m.Name != "AddMoney") continue;
                                var ps = m.GetParameters();
                                if (ps.Length != 2) continue;
                                if (ps[0].ParameterType != typeof(int)) continue;
                                if (!ps[1].ParameterType.IsAssignableFrom(playerType)) continue;
                                try { m.Invoke(inst, new object[] { total, localPlayer }); AfterCreditFx(total); return true; }
                                catch (Exception e) { Plugin.Log.LogDebug("AddMoney(int,Player) instância falhou: " + e.GetBaseException().Message); }
                            }
                        }
                    }
                    catch (Exception e) { Plugin.Log.LogDebug("MoneyManager instância (int,Player) falhou: " + e.GetBaseException().Message); }
                }
                else if (playerType != null)
                {
                    Plugin.Log.LogDebug("SellAll: Player local não resolvido; tentando fallbacks single-int.");
                }
                // 3) Fallbacks single-int (legado): estático AddMoney(int) / Add(int) / ...
                foreach (var mn in CreditMethodNames)
                {
                    var m = mmType.GetMethod(mn, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null, new[] { typeof(int) }, null);
                    if (m == null) continue;
                    try { m.Invoke(null, new object[] { total }); AfterCreditFx(total); return true; }
                    catch (Exception e) { Plugin.Log.LogDebug(mn + " falhou: " + e.GetBaseException().Message); }
                }
                // 4) Fallbacks single-int (legado): instância singleton.
                try
                {
                    object? inst = GetMoneyManagerInstance(mmType);
                    if (inst != null)
                    {
                        foreach (var mn in CreditMethodNames)
                        {
                            var m = mmType.GetMethod(mn, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { typeof(int) }, null);
                            if (m == null) continue;
                            m.Invoke(inst, new object[] { total });
                            AfterCreditFx(total);
                            return true;
                        }
                    }
                }
                catch (Exception e) { Plugin.Log.LogDebug("MoneyManager instância falhou: " + e.GetBaseException().Message); }
            }
            return false;
        }

        private static Type? FindPlayerType()
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    var t = asm.GetType("Player");
                    if (t != null) return t;
                }
                catch { }
            }
            Plugin.Log.LogDebug("SellAll: tipo Player não achado nos assemblies.");
            return null;
        }

        private static object? ResolveLocalPlayer(Type playerType)
        {
            UnityEngine.Object[] all;
            try { all = UnityEngine.Object.FindObjectsOfType(playerType); }
            catch (Exception e) { Plugin.Log.LogDebug("FindObjectsOfType(Player) falhou: " + e.GetBaseException().Message); return null; }
            if (all == null || all.Length == 0) return null;
            foreach (var o in all)
            {
                if (o == null) continue;
                if (HasLocalFlag(o)) return o;
            }
            return all[0]; // sem flag local determinável: primeiro achado
        }

        private static bool HasLocalFlag(object o)
        {
            var t = o.GetType();
            foreach (var n in LocalPlayerFlagNames)
            {
                try
                {
                    var f = t.GetField(n, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (f != null && f.FieldType == typeof(bool) && (bool)f.GetValue(o)!) return true;
                    var p = t.GetProperty(n, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (p != null && p.PropertyType == typeof(bool) && p.CanRead && (bool)p.GetValue(o, null)!) return true;
                }
                catch { }
            }
            return false;
        }

        private static object? GetMoneyManagerInstance(Type mmType)
        {
            var instProp = mmType.GetProperty("Instance", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (instProp != null)
            {
                try
                {
                    var v = instProp.GetValue(null, null);
                    if (v != null) return v;
                }
                catch { }
            }
            var f = mmType.GetField("Instance", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (f != null)
            {
                try
                {
                    var v = f.GetValue(null);
                    if (v != null) return v;
                }
                catch { }
            }
            try
            {
                var found = UnityEngine.Object.FindObjectOfType(mmType) as Component;
                if (found != null) return found;
            }
            catch { }
            return null;
        }

        private static void AfterCreditFx(int total)
        {
            try
            {
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    Type? ui = null;
                    try { ui = asm.GetType("PlayerUI"); } catch { continue; }
                    if (ui == null) continue;
                    // Invocar PlayerUI.SetMoney SOMENTE se existir overload single-int;
                    // senão pular (OnChangeMoney atualiza o HUD automaticamente).
                    var m = ui.GetMethod("SetMoney", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance, null, new[] { typeof(int) }, null);
                    if (m != null)
                    {
                        try
                        {
                            if (m.IsStatic) m.Invoke(null, new object[] { total });
                            else
                            {
                                var found = UnityEngine.Object.FindObjectOfType(ui) as Component;
                                if (found != null) m.Invoke(found, new object[] { total });
                            }
                        }
                        catch (Exception e) { Plugin.Log.LogDebug("SetMoney falhou: " + e.GetBaseException().Message); }
                    }
                    // MoneySound best-effort em try/catch.
                    try
                    {
                        Type? mm = null;
                        try { mm = asm.GetType("MoneyManager"); } catch { }
                        if (mm != null)
                        {
                            foreach (var snd in mm.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance))
                            {
                                if (snd.Name != "MoneySound") continue;
                                if (snd.GetParameters().Length != 0) continue; // overloads (bool,Player) exigem Player: pular com segurança
                                if (snd.IsStatic) snd.Invoke(null, null);
                                else
                                {
                                    var foundMm = UnityEngine.Object.FindObjectOfType(mm) as Component;
                                    if (foundMm != null) snd.Invoke(foundMm, null);
                                }
                                break;
                            }
                        }
                    }
                    catch (Exception e) { Plugin.Log.LogDebug("MoneySound falhou: " + e.GetBaseException().Message); }
                    break;
                }
            }
            catch (Exception e) { Plugin.Log.LogDebug("FX pós-crédito falhou: " + e.GetBaseException().Message); }
            Plugin.Log.LogInfo("SellAll: creditado $" + total);
        }
    }
}
