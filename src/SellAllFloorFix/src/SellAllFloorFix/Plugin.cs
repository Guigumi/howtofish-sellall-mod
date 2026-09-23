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
                int total = 0;
                int comVendaNativa = 0;
                var paraDespawner = new System.Collections.Generic.List<GroundItem>(items.Count);
                foreach (var gi in items)
                {
                    if (gi.Worth > 0 && Seller.TryNativeSell(gi.Component))
                    {
                        comVendaNativa++;
                        continue; // venda nativa já cuidou de dinheiro+despawn
                    }
                    total += gi.Worth;
                    paraDespawner.Add(gi);
                }
                if (total > 0) Seller.Credit(total);
                _running = true;
                StartCoroutine(Despawner.DespawnBatch(paraDespawner, Cfg.LimitePorFrame.Value, done =>
                {
                    _running = false;
                    Log.LogInfo("SellAll: " + items.Count + " itens (" + comVendaNativa + " venda nativa), $" + total + ", " + done + " despawnados.");
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
