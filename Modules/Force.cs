using System.Collections;
using System.Collections.Generic;
using ExtensionMethods;
using ThunderRoad;
using UnityEngine;

namespace Wand; 

public class Force : WandSkill {
    public override void Register() {
        base.Register();
        wand.button.Then(() => wand.holdingHand.Velocity().IsFacing(wand.holdingHand.PalmDir())
                               && wand.otherHand.Velocity().IsFacing(wand.otherHand.PalmDir())
                               && wand.otherHand.Velocity().IsFacing(wand.holdingHand.Velocity(), 30)
                               && Hands.AverageVelocity().magnitude > wand.module.gestureVelocitySmall,
                "Dual Hand Push")
            .Do(ForcePush, "Force Push");
    }
        
    public void ForcePush() {
        MarkCasted();
        wand.PlaySound(SoundType.Docgh);
        var handMidpoint = Hands.Midpoint();
        var direction = Vector3.Slerp(Hands.AveragePalm(), Player.local.head.transform.forward, 0.5f)
            .normalized;
        var hitRBs = new HashSet<Rigidbody>();
        var shockwavePoint = handMidpoint + direction * 1.5f;
        wand.module.SpawnShockwave(shockwavePoint, Player.local.head.transform.position - shockwavePoint);
        wand.holdingHand.HapticTick();
        wand.otherHand.HapticTick();
        wand.module.shoveEffectData.Spawn(shockwavePoint, Quaternion.identity).Play();
        var hits = Physics.OverlapBox(handMidpoint + direction * 3, new Vector3(2, 2, 3f),
            Quaternion.LookRotation(direction));
        for (var index = 0; index < hits.Length; index++) {
            var hit = hits[index];
            if (hit.attachedRigidbody is Rigidbody rb && !hitRBs.Contains(hit.attachedRigidbody)) {
                hitRBs.Add(hit.attachedRigidbody);
                if (rb.GetComponentInParent<Player>() != null) continue;
                if (rb.GetComponentInParent<Creature>() is Creature creature) {
                    creature.TryPush(Creature.PushType.Magic, direction, 3);
                    if (!creature.isKilled)
                        creature.ragdoll.SetState(Ragdoll.State.Destabilized);
                    creature.Inflict("Floating", this, 3);
                }

                if (rb.GetComponentInParent<Item>() is Item hitItem) {
                    if (hitItem.mainHandler?.creature?.isPlayer == true) continue;
                    hitItem.Inflict("Floating", this, 3);
                }
                rb.AddForce(
                    direction
                    * (wand.module.forceAmount
                       * rb.GetMassModifier()
                       * Vector3.Distance(hit.attachedRigidbody.transform.position, handMidpoint).Remap01(6, 0)),
                    ForceMode.Impulse);
            }
        }
    }
}