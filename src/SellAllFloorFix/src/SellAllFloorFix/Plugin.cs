using System.Collections;
using BepInEx;
using BepInEx.Logging;
using UnityEngine;

namespace SellAllFloorFix
{
    [BepInPlugin("Guigumi.SellAllFloorFix", "SellAllFloorFix", "1.0.0")]
    public sealed class Plugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log = null!;
        internal SellAllConfig Cfg = null!;
        private float _lastRun = -999f;
        private bool _running;

        private void Awake()
        {
            Log = Logger;
            Cfg = new SellAllConfig(Config);
            Log.LogInfo("SellAllFloorFix v1.0.0 loaded. Tecla: " + Cfg.Tecla.Value);
        }

        private void OnDisable()
        {
            _running = false;
        }

        private void OnDestroy()
        {
            _running = false;
        }

        private void Update()
        {
            if (Cfg.Tecla.Value.IsDown())
            {
                if (_running) return;
                if (Time.realtimeSinceStartup - _lastRun < Cfg.CooldownSeg.Value) return;
                _lastRun = Time.realtimeSinceStartup;
                TrySellAll();
            }
        }

        private void TrySellAll()
        {
            try
            {
                if (!Despawner.IsServer())
                {
                    Log.LogInfo("SellAll: apenas o host pode vender (v1). Peça ao host para apertar " + Cfg.Tecla.Value + ".");
                    return;
                }
                var items = ItemCollector.Collect(Cfg.SoVendaveis.Value, Cfg.IncluirSemValor.Value);
                Log.LogInfo("SellAll scan: " + items.Count + " candidatos.");
                if (items.Count == 0)
                {
                    Log.LogInfo("SellAll: nada para vender.");
                    return;
                }
                // Acoplamento crédito↔despawn (spec §6, ambas as direções):
                // - crédito ok (nativo ou fallback) → entra no despawn;
                // - sem crédito → NUNCA despawna (vai p/ falhas, item mantido).
                int nativeTotal = 0;
                int nativeCount = 0;
                int fallbackTotal = 0;
                var fallbackItems = new System.Collections.Generic.List<GroundItem>(items.Count);
                var paraDespawner = new System.Collections.Generic.List<GroundItem>(items.Count);
                var falhas = new System.Collections.Generic.List<string>();
                foreach (var gi in items)
                {
                    if (gi.Worth > 0)
                    {
                        if (Seller.TryNativeSell(gi.Component))
                        {
                            nativeCount++;
                            nativeTotal += gi.Worth;
                            paraDespawner.Add(gi);
                        }
                        else
                        {
                            fallbackTotal += gi.Worth;
                            fallbackItems.Add(gi);
                        }
                    }
                    else if (Cfg.IncluirSemValor.Value)
                    {
                        paraDespawner.Add(gi); // limpeza, sem crédito
                    }
                    // else: skip (sem valor e sem flag de limpeza)
                }
                int creditedFallback = 0;
                if (fallbackItems.Count > 0)
                {
                    if (Seller.Credit(fallbackTotal))
                    {
                        creditedFallback = fallbackTotal;
                        paraDespawner.AddRange(fallbackItems);
                    }
                    else
                    {
                        foreach (var f in fallbackItems)
                        {
                            string fname = f.Go != null ? f.Go.name : "?";
                            Log.LogWarning("SellAll: sem crédito p/ '" + fname + "' ($" + f.Worth + "); item MANTIDO no chão.");
                            falhas.Add(fname);
                        }
                    }
                }
                if (paraDespawner.Count == 0)
                {
                    string msg0 = "SellAll: " + items.Count + " itens (" + nativeCount + " nativa), $" + nativeTotal + "+$" + creditedFallback + ", 0 despawnados";
                    if (falhas.Count > 0) msg0 += ", " + falhas.Count + " falhas";
                    Log.LogInfo(msg0 + ".");
                    return;
                }
                _running = true;
                int n = items.Count, m = nativeCount, nt = nativeTotal, ft = creditedFallback;
                var fails = new System.Collections.Generic.List<string>(falhas);
                StartCoroutine(Despawner.DespawnBatch(paraDespawner, Cfg.LimitePorFrame.Value, done =>
                {
                    _running = false;
                    string msg = "SellAll: " + n + " itens (" + m + " nativa), $" + nt + "+$" + ft + ", " + done + " despawnados";
                    if (fails.Count > 0) msg += ", " + fails.Count + " falhas";
                    Log.LogInfo(msg + ".");
                }));
            }
            catch (System.Exception e)
            {
                _running = false;
                Log.LogError("SellAll falhou: " + e);
            }
        }
    }
}
