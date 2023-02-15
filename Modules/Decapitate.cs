using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ExtensionMethods;
using ThunderRoad;
using UnityEngine;

namespace Wand; 

public class Decapitate : WandModule {
    public override void OnInit() {
        base.OnInit();

        wand.targetedEnemy
            .Then(wand.Flick(AxisDirection.Left, wand.module.gestureVelocityLarge * 1.5f),
                wand.Flick(AxisDirection.Right, wand.module.gestureVelocityLarge * 1.5f))
            .Do(SliceEntity, "Slice Entity");
        wand.targetedEnemy
            .Then(wand.Flick(AxisDirection.Right, wand.module.gestureVelocityLarge * 1.5f), wand
                .Flick(AxisDirection.Left, wand.module.gestureVelocityLarge * 1.5f))
            .Do(SliceEntity, "Slice Entity");
    }

    public void SliceEntity() {
        if (wand.target?.creature == null) {
            wand.Reset();
            return;
        }
        
        MarkCasted();

        wand.target.creature.ragdoll.GetPart(RagdollPart.Type.Neck).TrySlice();
        wand.RunAfter(
            () => {
                wand.target.creature.ragdoll.headPart.rb.AddForce(Vector3.up * 5f, ForceMode.VelocityChange);
            }, 0.1f);
        wand.target.creature.Kill();
        wand.canRestart = true;
    }

}