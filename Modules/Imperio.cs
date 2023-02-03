using SequenceTracker;
using ExtensionMethods;
using ThunderRoad;
using UnityEngine;
using GestureEngine;

namespace Wand; 

public class Imperio : WandModule {
    public Step imperio;

    public override void OnInit() {
        imperio = wand.targetedEnemy
            .Then(wand.Offhand.Palm(Direction.Forward).Point(Direction.Up).Fist)
            .Do(() => wand.RunAfter(() => ControlEntity(wand), 0.3f));

        imperio.Then(wand.Brandish())
            .Do(AttackOrder, "Attack Order");
            
        imperio.Then(wand.Offhand.Moving(Direction.Forward).Palm(Direction.Forward).Open)
            .Do(PacifyOrder, "Pacify Order");

        imperio.Then(wand.Swirl(SwirlDirection.Clockwise))
            .Do(MimicOrder, "Mimic Order");
    }

    public static void ControlEntity(WandBehaviour wand) {
        if (wand.target?.creature == null) {
            wand.Reset();
            return;
        }

        wand.target.creature.brain.isManuallyControlled = false;

        wand.swirlAngle = 0;
    }

    public void AttackOrder() {
        if (wand.target?.creature == null) {
            wand.Reset();
            return;
        }
            
        var creature = Utils.TargetCreature(wand.tipRay, 15, 40, null, false);
        if (creature == null) return;
        wand.PlaySound(SoundType.Hagh, creature.transform);
        wand.target.creature.SetFaction(2);
        var line = wand.module.targetLineEffectData.Spawn(wand.transform);
        line.SetSource(wand.tip);
        line.SetTarget(creature.GetTorso().transform);
        line.SetMainGradient(wand.module.attackOrderGradient);

        line.Play();
        wand.module.castEffectData.Spawn(wand.tip).Play();
        wand.module.targetEffectData.Spawn(wand.target.transform).Play();
        wand.target.creature.brain.currentTarget = creature;
    }

    public void PacifyOrder() {
        if (wand.target?.creature == null) {
            wand.Reset();
            return;
        }
            
        wand.PlaySound(SoundType.Foll, wand.target.creature.transform);
            
        wand.target.creature.handLeft?.TryRelease();
        wand.target.creature.handRight?.TryRelease();
        wand.target.creature.brain.SetState(Brain.State.Idle);
        wand.target.creature.brain.currentTarget = null;
        wand.target.creature.SetFaction(1);
    }

    public void MimicOrder() {
        if (wand.target?.creature == null) {
            wand.Reset();
            return;
        }
            
        wand.PlaySound(SoundType.Quough, wand.target.creature.transform);

        wand.target.creature.gameObject.GetOrAddComponent<MimicBehaviour>().Init(this);
    }
}

public class MimicBehaviour : MonoBehaviour {
    private Imperio spell;
    private Creature creature;

    private Transform handLeftTarget;
    private Transform handRightTarget;
    private Transform headTarget;
    private Transform hipsTarget;

    public void Init(Imperio spell) {
        this.spell = spell;
        handLeftTarget = new GameObject().transform;
        handRightTarget = new GameObject().transform;
        headTarget = new GameObject().transform;
        hipsTarget = new GameObject().transform;
        creature = GetComponent<Creature>();
            
        creature.brain.isManuallyControlled = true;
        creature.brain.SetState(Brain.State.Custom);
        creature.ragdoll.AddPhysicToggleModifier(this);

        creature.OnDespawnEvent += time => {
            if (time == EventTime.OnStart) {
                Reset();
                Destroy(this);
            }
        };

        creature.handLeft.grabbedHandle?.item.ResetRagdollCollision();
        creature.handLeft.grabbedHandle?.item.ResetObjectCollision();
        creature.handLeft.grabbedHandle?.item.SetColliderLayer(GameManager.GetLayer(LayerName.MovingItem));
            
        creature.handRight.grabbedHandle?.item.ResetRagdollCollision();
        creature.handRight.grabbedHandle?.item.ResetObjectCollision();
        creature.handRight.grabbedHandle?.item.SetColliderLayer(GameManager.GetLayer(LayerName.MovingItem));
            
        creature.animator.enabled = false;
        creature.animator.cullingMode = AnimatorCullingMode.CullCompletely;
            
        creature.ragdoll.ik.SetHandAnchor(Side.Right, handRightTarget);
        creature.ragdoll.ik.SetHandState(Side.Right, true, true);
            
        creature.ragdoll.ik.SetHandAnchor(Side.Left, handLeftTarget);
        creature.ragdoll.ik.SetHandState(Side.Left, true, true);
            
        creature.ragdoll.ik.SetHeadAnchor(headTarget);
        creature.ragdoll.ik.SetHeadState(true, true);
            
        creature.ragdoll.ik.SetHipsAnchor(hipsTarget);
        creature.ragdoll.ik.SetHipsState(true);
            
        creature.ragdoll.DisableJointLimit();
    }

    public void Reset() {
        creature.ragdoll.ik.Setup();
        creature.brain.isManuallyControlled = false;
        creature.brain.SetState(Brain.State.Idle);
        creature.animator.enabled = true;
        Destroy(handLeftTarget);
        Destroy(handRightTarget);
        Destroy(headTarget);
        Destroy(hipsTarget);
    }

    private void Update() {
        SetIKTarget(handLeftTarget, Player.currentCreature.ragdoll.ik.handLeftTarget);
        SetIKTarget(handRightTarget, Player.currentCreature.ragdoll.ik.handRightTarget);
        SetIKTarget(headTarget, Player.currentCreature.ragdoll.ik.headTarget);
        SetIKTarget(hipsTarget, Player.currentCreature.GetTorso().transform);
    }

    public void SetIKTarget(Transform target, Transform playerTarget) {
        if (target == null || playerTarget == null || Player.currentCreature?.ragdoll == null) {
            return;
        }
        target.SetPositionAndRotation(
            creature.ragdoll.transform.TransformPoint(
                Player.currentCreature.ragdoll.transform.InverseTransformPoint(playerTarget.position)),
            creature.ragdoll.transform.rotation
            * (Quaternion.Inverse(Player.currentCreature.ragdoll.transform.rotation)
               * playerTarget.rotation));
    }
}