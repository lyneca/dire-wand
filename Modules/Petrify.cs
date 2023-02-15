using ExtensionMethods;
using GestureEngine;
using ThunderRoad;
using UnityEngine;

namespace Wand; 

public class Petrify : WandModule {
    public override void OnInit() {
        base.OnInit();
        wand.targetedItem
            .Then(Gesture.Both.Palm(Direction.Forward).Moving(Direction.Forward))
            .Do(PetrifyEntity, "Freeze Item");
        wand.targetedEnemy
            .Then(Gesture.Both.Palm(Direction.Forward).Moving(Direction.Forward))
            .Do(PetrifyEntity, "Petrify Enemy");
    }

    public void PetrifyEntity() {
        MarkCasted();
        wand.PlaySound(SoundType.Quough, wand.target.Transform);
        if (wand.target?.creature is Creature creature) {
            creature.gameObject.GetOrAddComponent<ParalysisModifier>().AddHandler(this);
            wand.module.freezeEffectData.Spawn(wand.target.Transform).Play();
            wand.canRestart = true;
        } else if (wand.target?.item is Item item) {
            item.gameObject.GetComponent<BounceBehaviour>()?.Deactivate();
            item.gameObject.GetOrAddComponent<FreezeModifier>().AddHandler(this);
            wand.module.freezeEffectData.Spawn(wand.target.Transform).Play();
            wand.canRestart = true;
        }
    }
}

public class FreezeModifier : ItemModifier {
    private FixedJoint joint;
    public override void OnApply() {
        base.OnApply();
        if (joint)
            Destroy(joint);
        joint = item.gameObject.AddComponent<FixedJoint>();
        joint.connectedAnchor = item.transform.position;
    }

    public override void OnRemove() {
        base.OnRemove();
        if (joint)
            Destroy(joint);
    }
}

public class ParalysisModifier : CreatureModifier {
    public override void OnBegin() {
        base.OnBegin();
        creature.OnKillEvent += (_, __) => Clear();
        creature.OnDespawnEvent += _ => Clear();
    }

    public override void OnApply() {
        base.OnApply();
        creature.ragdoll.SetState(Ragdoll.State.Destabilized);
        creature.brain.AddNoStandUpModifier(this);
        for (var index = 0; index < creature.ragdoll.parts.Count; index++) {
            var part = creature.ragdoll.parts[index];
            part.FreezeCharacterJoint();
        }
    }

    public override void OnRemove() {
        base.OnRemove();
        creature.brain.RemoveNoStandUpModifier(this);
        for (var index = 0; index < creature.ragdoll.parts.Count; index++) {
            var part = creature.ragdoll.parts[index];
            part.UnfreezeCharacterJoint();
        }
    }
}