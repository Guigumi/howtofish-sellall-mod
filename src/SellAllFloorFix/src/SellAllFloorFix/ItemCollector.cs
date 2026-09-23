using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace SellAllFloorFix
{
    internal readonly struct GroundItem
    {
        public readonly GameObject Go;
        public readonly int Worth;
        public readonly Component Component;
        public readonly object? NetObj;

        public GroundItem(GameObject go, int worth, Component comp, object? netObj)
        {
            Go = go;
            Worth = worth;
            Component = comp;
            NetObj = netObj;
        }
    }

    internal static class ItemCollector
    {
        private static readonly string[] ItemTypeNames = { "Item", "Creature" };
        private static readonly string[] WorthNames = { "TotalWorth", "DefaultWorth", "Worth", "worth", "price", "Price", "value", "Value", "sellPrice", "SellPrice" };
        private static readonly string[] HeldNames = { "HasPlayerHolder", "IsInInventory", "isHeld", "IsHeld", "held", "Held", "inHand", "InHand", "equipped", "Equipped" };

        public static List<GroundItem> Collect(bool soVendaveis, bool incluirSemValor)
        {
            var out_ = new List<GroundItem>(128);
            var behaviours = UnityEngine.Object.FindObjectsOfType<MonoBehaviour>();
            foreach (var b in behaviours)
            {
                if (b == null) continue;
                var t = b.GetType();
                if (!IsItemType(t)) continue;
                var go = b.gameObject;
                if (go == null || !go.activeInHierarchy) continue;
                if (IsHeld(b)) continue;
                int worth = ReadWorth(b);
                if (soVendaveis && worth <= 0 && !incluirSemValor) continue;
                object? netObj = FindNetworkObject(go);
                out_.Add(new GroundItem(go, Math.Max(0, worth), b, netObj));
            }
            return out_;
        }

        private static bool IsItemType(Type t)
        {
            string name = t.Name;
            foreach (var n in ItemTypeNames)
                if (name == n || t.FullName == n)
                    return true;
            // Herança: Creature costuma herdar de Item — aceita subclasses cujo base se chame Item
            var bt = t.BaseType;
            while (bt != null && bt != typeof(object) && bt != typeof(MonoBehaviour) && bt != typeof(Component))
            {
                if (bt.Name == "Item") return true;
                bt = bt.BaseType;
            }
            return false;
        }

        private static bool IsHeld(Component c)
        {
            var t = c.GetType();
            foreach (var n in HeldNames)
            {
                var f = t.GetField(n, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (f != null && f.FieldType == typeof(bool))
                {
                    try { if ((bool)f.GetValue(c)!) return true; } catch { }
                }
                var p = t.GetProperty(n, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (p != null && p.PropertyType == typeof(bool) && p.CanRead)
                {
                    try { if ((bool)p.GetValue(c, null)!) return true; } catch { }
                }
            }
            // Segurança extra: se o objeto está parentado a um Player, considera segurado
            var parent = c.transform?.parent;
            while (parent != null)
            {
                if (parent.name.IndexOf("player", StringComparison.OrdinalIgnoreCase) >= 0
                    || parent.name.IndexOf("hand", StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
                parent = parent.parent;
            }
            return false;
        }

        private static int ReadWorth(Component c)
        {
            var t = c.GetType();
            foreach (var n in WorthNames)
            {
                var f = t.GetField(n, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (f != null)
                {
                    try
                    {
                        object? v = f.GetValue(c);
                        if (v is int i) return i;
                        if (v is float fl) return Mathf.RoundToInt(fl);
                        if (v is double d) return (int)Math.Round(d);
                    }
                    catch { }
                }
                var p = t.GetProperty(n, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (p != null && p.CanRead)
                {
                    try
                    {
                        object? v = p.GetValue(c, null);
                        if (v is int i) return i;
                        if (v is float fl) return Mathf.RoundToInt(fl);
                        if (v is double d) return (int)Math.Round(d);
                    }
                    catch { }
                }
            }
            return 0;
        }

        private static object? FindNetworkObject(GameObject go)
        {
            var comps = go.GetComponents<Component>();
            foreach (var c in comps)
            {
                if (c == null) continue;
                if (c.GetType().Name == "NetworkObject")
                    return c;
            }
            return null;
        }
    }
}
