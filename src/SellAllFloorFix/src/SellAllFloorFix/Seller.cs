using System;
using System.Reflection;
using UnityEngine;

namespace SellAllFloorFix
{
    internal static class Seller
    {
        private static readonly string[] SellMethodNames = { "Sell", "SellItem", "CmdSell", "ServerSell", "SellToShop" };
        private static readonly string[] CreditMethodNames = { "AddMoney", "Add", "GiveMoney", "AddCoins" };

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

        public static void Credit(int total)
        {
            if (total <= 0) return;
            if (TryCreditViaMoneyManager(total)) return;
            Plugin.Log.LogWarning("SellAll: nenhum MoneyManager achado; dinheiro NÃO creditado (itens ainda serão despawnados).");
        }

        private static bool TryCreditViaMoneyManager(int total)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type? mmType = null;
                try { mmType = asm.GetType("MoneyManager"); } catch { continue; }
                if (mmType == null) continue;
                // 1) método estático single-int AddMoney(int) / Add(int) / GiveMoney(int) / AddCoins(int).
                // NUNCA chamar overloads (int, Player): Player local não resolvível com segurança.
                foreach (var mn in CreditMethodNames)
                {
                    var m = mmType.GetMethod(mn, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null, new[] { typeof(int) }, null);
                    if (m == null) continue;
                    try { m.Invoke(null, new object[] { total }); AfterCreditFx(total); return true; }
                    catch (Exception e) { Plugin.Log.LogDebug(mn + " falhou: " + e.GetBaseException().Message); }
                }
                // 2) instância singleton single-int: Instance.AddMoney(total)
                try
                {
                    object? inst = null;
                    var instProp = mmType.GetProperty("Instance", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                    if (instProp != null) inst = instProp.GetValue(null, null);
                    if (inst == null)
                    {
                        var f = mmType.GetField("Instance", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                        if (f != null) inst = f.GetValue(null);
                    }
                    if (inst == null)
                    {
                        var found = UnityEngine.Object.FindObjectOfType(mmType) as Component;
                        if (found != null) inst = found;
                    }
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
