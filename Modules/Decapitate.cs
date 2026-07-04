using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ExtensionMethods;
using ThunderRoad;
using UnityEngine;

namespace Wand; 

public class Decapitate : WandSkill {
    public override void OnInit() {
        base.OnInit();

        wand.profane
            .Then(wand.Flick(AxisDirection.Left, wand.module.gestureVelocityLarge * 1.5f),
                wand.Flick(AxisDirection.Right, wand.module.gestureVelocityLarge * 1.5f))
            .Do(SliceEntity, "Slice Entity");
        wand.profane
            .Then(wand.Flick(AxisDirection.Right, wand.module.gestureVelocityLarge * 1.5f), wand
                .Flick(AxisDirection.Left, wand.module.gestureVelocityLarge * 1.5f))
            .Do(SliceEntity, "Slice Entity");
    }

    public void SliceEntity() {
        if (wand.target is not Creature creature) {
            wand.Reset();
            return;
        }
        
        MarkCasted();

        creature.ragdoll.GetPart(RagdollPart.Type.Neck).TrySlice();
        wand.RunAfter(
            () => {
                creature.ragdoll.headPart.physicBody.AddForce(Vector3.up * 5f, ForceMode.VelocityChange);
            }, 0.1f);
        creature.Kill();
        wand.canRestart = true;
    }

}