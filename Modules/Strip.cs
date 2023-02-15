using System;
using ExtensionMethods;
using GestureEngine;
using ThunderRoad;
using UnityEngine;
using UnityEngine.Experimental.Audio;

namespace Wand; 

public class Strip : WandModule {
    public override void OnInit() {
        base.OnInit();
        wand.targetedEnemy
            .Then(wand.Offhand.Palm(Direction.Inwards).Moving(Direction.Outwards, 2.5f).Open)
            .Then(wand.Offhand.Palm(Direction.Inwards).Moving(Direction.Inwards, 2.5f).Open)
            .Do(StripEnemy, "Strip Armor");
    }

    public class ArmorSwapper : MonoBehaviour {
        public Creature creature;
        public void Start() {
            creature = GetComponentInParent<Creature>();
            creature.OnDespawnEvent += time => {
                if (time == EventTime.OnStart) Destroy(this);
            };
        }
        public void Update() {
            if ((creature?.handLeft?.grabbedHandle?.item?.data.HasModule<ItemModuleWardrobe>() ?? false)
                || (creature?.handRight?.grabbedHandle?.item?.data.HasModule<ItemModuleWardrobe>() ?? false)
                || (Mirror.local != null
                    && Mirror.local.allowArmourEditing
                    && Mirror.local.mirrorMesh.isVisible
                    && Mirror.local.isRendering)) {
                Player.currentCreature.equipment.canSwapExistingArmour = true;
                Player.currentCreature.equipment.armourEditModeEnabled = true;
            } else {
                Player.currentCreature.equipment.canSwapExistingArmour = false;
                Player.currentCreature.equipment.armourEditModeEnabled = false;
            }
        }
    }

    public void StripEnemy() {
        if (!wand.target.isCreature) return;
        MarkCasted();
        Player.currentCreature.gameObject.GetOrAddComponent<ArmorSwapper>();
        for (var i = 0; i < wand.target.creature.ragdoll.parts.Count; i++) {
            var part = wand.target.creature.ragdoll.parts[i];
            if (part.isSliced) return;
        }

        for (var i = 0; i < wand.target.creature.equipment.wearableSlots.Count; i++) {
            var slot = wand.target.creature.equipment.wearableSlots[i];
            if (slot == null) {
                continue;
            }
            for (var j = 0; j < slot.wardrobeLayers.Length; j++) {
                if (slot.wardrobeLayers[j].layer == null) continue;
                slot.UnEquip(slot.wardrobeLayers[j].layer, item => {
                    var force = (slot.Part.transform.position - wand.target.WorldCenter).normalized;
                    item.transform.position = slot.Part.transform.position
                                              + (slot.Part.transform.position - wand.target.WorldCenter).normalized
                                              * 0.1f;
                    var creature = wand.target.creature;
                    item.IgnoreRagdollCollision(creature.ragdoll);
                    item.RunAfter(item.ResetRagdollCollision, 0.8f);
                    item.rb.AddForce(force * 8, ForceMode.VelocityChange);
                });
            }
        }
    }
}
