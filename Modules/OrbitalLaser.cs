using System.Collections;
using System.ComponentModel.Design;
using ExtensionMethods;
using ThunderRoad;
using UnityEngine;

namespace Wand {
    public class OrbitalLaser : WandModule {
        private float targetIntensity = 0;
        private float actualIntensity = 0;
        
        public string chargeEffectId = "WandLaserCharge";
        private EffectData chargeEffectData;

        private EffectInstance chargeEffect;
        private bool active;
        
        public override void OnInit() {
            base.OnInit();
            chargeEffectData = Catalog.GetData<EffectData>(chargeEffectId);
            //wand.button
            //    .Then(() => wand.tip.forward.IsFacing(Vector3.up), wand.Swirl(SwirlDirection.Either, 1))
            //    .Do(() => Phase(0))
            //    .Then(() => wand.tip.forward.IsFacing(Vector3.up), wand.Swirl(SwirlDirection.Either, 2))
            //    .Do(() => Phase(1))
            //    .Then(() => wand.tip.forward.IsFacing(Vector3.up), wand.Swirl(SwirlDirection.Either, 3))
            //    .Do(() => Phase(2))
            //    .Then(() => wand.tip.forward.IsFacing(Vector3.up), wand.Swirl(SwirlDirection.Either, 4))
            //    .Do(() => Phase(3))
            //    .Then(() => wand.tip.forward.IsFacing(Vector3.up), wand.Swirl(SwirlDirection.Either, 5))
            //    .Do(() => Phase(4))
            //    .Then(() => wand.tip.forward.IsFacing(Vector3.up), wand.Swirl(SwirlDirection.Either, 6))
            //    .Do(() => Phase(5))
            //    .Then(wand.Brandish())
            //    .Do(Beam);
        }

        public void Phase(int level) {
            if (level == 0) {
                    active = true;
                    wand.StartCoroutine(Loop());
                    SpawnEffect();
                    actualIntensity = 0;
            }
            targetIntensity = 1 / 6f * (level + 1);
        }

        public override void OnReset() {
            End();
        }

        public IEnumerator Loop() {
            while (active) {
                actualIntensity = Mathf.Lerp(actualIntensity, targetIntensity, Time.deltaTime * 10);
                chargeEffect?.SetIntensity(actualIntensity);
                yield return 0;
            }
        }

        public void End() {
            active = false;
            chargeEffect?.Despawn();
            targetIntensity = actualIntensity = 0;
        }
        
        public void SpawnEffect() {
            chargeEffect?.Despawn();
            chargeEffect = chargeEffectData.Spawn(wand.transform);
            chargeEffect.SetIntensity(0);
            chargeEffect.Play();
        }
        public void Beam() {}
    }
}
