using System;
using System.Collections;
using ExtensionMethods;
using GestureEngine;
using ThunderRoad;
using UnityEngine;

namespace Wand {
    public class Hover : WandModule {
        private float upwardForce = 1;
        private float hoverDuration = 10;

        private Coroutine floatRoutine;
        private Coroutine pullRoutine;

        public override void OnInit() {
            base.OnInit();

            wand.OnTargetEntity(state => {
                var hover = wand.Offhand.Moving(Direction.Up).Palm(Direction.Up).Gripping;
                var slam = wand.Offhand.Moving(Direction.Down).Palm(Direction.Down).Gripping;

                var hovered = state
                    .Then(hover)
                    .Do(HoverEntity, "Hover Entity");
                
                var pulled = hovered
                    .Then(wand.Offhand.Moving(Direction.Backward).Gripping)
                    .Do(PullEntity, "Pull Entity");
                
                state
                    .Then(slam)
                    .Do(SlamEntity, "Slam Entity");
                hovered
                    .Then(slam)
                    .Do(SlamEntity, "Slam Entity");
                pulled
                    .Then(slam)
                    .Do(SlamEntity, "Slam Entity");
            });

        }

        public void PullEntity() {
            pullRoutine = wand.target.PullTowards(Player.local.head.transform.position
                                                         + Vector3.ProjectOnPlane(Player.local.head.transform.forward, Vector3.up).normalized
                                                         * 2
                                                         + Vector3.up);
        }
        public void HoverEntity() {
            floatRoutine = wand.target.StartCoroutine(HoverRoutine(wand.target));
        }

        public void SlamEntity() {
            if (pullRoutine != null) wand.target.StopCoroutine(pullRoutine);
            wand.target.Release();
            wand.target.Rigidbody().AddForce(Vector3.down * (4 * upwardForce * (wand.target.isCreature ? 30 : 1)),
                ForceMode.VelocityChange);
        }

        public IEnumerator HoverRoutine(Entity entity) {
            entity.Grab(false);
            float startTime = Time.time;
            entity.Rigidbody().AddForce(Vector3.up * (upwardForce * (entity.isCreature ? 30 : 1)), ForceMode.VelocityChange);
            yield return Utils.LoopOver(amount => {
                entity.SetPhysicModifier(entity, gravity: 0, drag: 2 * amount, angularDrag: 3 * amount);
            }, 2);
            while (entity.grabbed && entity.creature?.isKilled != true && Time.time - startTime < hoverDuration)
                yield return 0;
            if (!entity.grabbed)
                entity.Release();
        }
    }
}
