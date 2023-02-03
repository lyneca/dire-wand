﻿using System.Collections;
using ExtensionMethods;
using GestureEngine;
using ThunderRoad;
using UnityEngine;

namespace Wand {
    public class Hover : WandModule {
        private float upwardForce = 3;
        private float hoverDuration = 10;

        private Coroutine floatRoutine;
        private Coroutine pullRoutine;

        public override void OnInit() {
            base.OnInit();

            wand.OnTargetEntity(state => {

                var hovered = state
                    .Then(wand.Offhand.Moving(Direction.Up).Palm(Direction.Up).Gripping)
                    .Do(HoverEntity, "Hover Entity");
                
                var pulled = hovered
                    .Then(wand.Offhand.Moving(Direction.Backward).Gripping)
                    .Do(PullEntity, "Pull Entity");
                
                hovered
                    .Then(wand.Offhand.Moving(Direction.Down).Palm(Direction.Down).Gripping)
                    .Do(SlamEntity, "Slam Entity");
                pulled
                    .Then(wand.Offhand.Moving(Direction.Down).Palm(Direction.Down).Gripping)
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
            wand.target.StopCoroutine(floatRoutine);
            if (pullRoutine != null) wand.target.StopCoroutine(pullRoutine);
            wand.target.Release();
            wand.target.Rigidbody().AddForce(Vector3.down * (4 * upwardForce * (wand.target.isCreature ? 30 : 1)),
                ForceMode.VelocityChange);
        }

        public IEnumerator HoverRoutine(Entity entity) {
            entity.Grab(false);
            entity.Rigidbody().AddForce(Vector3.up * (upwardForce * (entity.isCreature ? 30 : 1)), ForceMode.VelocityChange);
            yield return Utils.LoopOver(amount => {
                entity.SetPhysicModifier(entity, gravity: 0, drag: 10 * amount, angularDrag: 10 * amount);
            }, 4);
            yield return new WaitForSeconds(hoverDuration);
            entity.Release();
        }
    }
}