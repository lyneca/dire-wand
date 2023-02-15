using ExtensionMethods;
using GestureEngine;
using ThunderRoad;
using ThunderRoad.AI.Action;
using UnityEngine;

namespace Wand; 

public class Scale : WandModule {
    public override void OnInit() {
        base.OnInit();
        var shrink = Gesture.Both
            .Moving(Direction.Together)
            .Gripping;
        var grow = Gesture.Both
            .Moving(Direction.Apart)
            .Gripping;

        wand.targetedItem.Then(shrink).Do(() => ScaleEntity(0.5f));
        wand.targetedItem.Then(grow).Do(() => ScaleEntity(2));
        wand.targetedEnemy.Then(shrink).Do(() => ScaleEntity(0.5f));
        wand.targetedEnemy.Then(grow).Do(() => ScaleEntity(2));
    }

    protected void ScaleEntity(float scale) {
        MarkCasted();
        ScaleHelper scaleHelper =
            wand.target.isCreature
                ? wand.target.creature.gameObject.GetOrAddComponent<CreatureScaleHelper>()
                : wand.target.item.gameObject.GetOrAddComponent<ItemScaleHelper>();
        wand.target.Rigidbody().AddForce(Vector3.up * 3, ForceMode.VelocityChange);
        scaleHelper.StartCoroutine(Utils.LoopOver(
            time => scaleHelper.Scale(Mathf.Lerp(1, scale, time.Curve(0, 0.6f, 0.5f, 1))),
            0.4f,
            () => scaleHelper.Set(scale)));
    }


    public abstract class ScaleHelper : MonoBehaviour {
        protected float currentScale = 1;
        protected float lowerScaleLimit = 1 / 4f;
        protected float upperScaleLimit = 4f;

        public abstract void Set(float scale);
        public abstract float Scale(float scale);
        public abstract void SetBounds(float lower, float upper);

        public abstract float CurrentRatio();

        public void ResetScale() {
            Set(Scale(1 / CurrentRatio()));
        }
    }
    
    public class ItemScaleHelper : ScaleHelper {
        public Item item;
        private float mass;
        private float originalMass;
        private float originalDataMass;
        private float originalDrag;
        private float originalAngularDrag;

        public void Awake() {
            item = GetComponent<Item>();
            originalMass = item.rb.mass;
            mass = originalMass;
            originalDataMass = item.data.mass;
            originalDrag = item.rb.drag;
            originalAngularDrag = item.rb.angularDrag;
        }

        public override void SetBounds(float lower, float upper) {
            lowerScaleLimit = lower;
            upperScaleLimit = upper;
        }

        public override float CurrentRatio() => currentScale;

        public override void Set(float scale) {
            currentScale = Mathf.Clamp(scale * currentScale, lowerScaleLimit, upperScaleLimit);
        }

        public float IncreaseMass(float original, float increase) {
            return Mathf.Pow(Mathf.Pow(original, 1f / 2.5f) * Mathf.Sqrt(increase), 2.5f);
        }

        public void Update() {
            item.rb.mass = mass;
            if (!item.isTelekinesisGrabbed)
                item.rb.ResetCenterOfMass();
        }

        public override float Scale(float scale) {
            float size = Mathf.Clamp(scale * currentScale, lowerScaleLimit, upperScaleLimit);
            item.transform.localScale = Vector3.one * size;
            item.rb.mass = IncreaseMass(originalMass, size);
            item.data.mass = IncreaseMass(originalDataMass, size);
            mass = item.rb.mass;
            item.rb.drag = originalDrag * size;
            item.rb.angularDrag = originalAngularDrag * size;
            item.rb.ResetCenterOfMass();
            item.handles?.ForEach(handle => {
                handle.CalculateReach();
            });
            item.RefreshCollision();
            return (size - lowerScaleLimit) / (upperScaleLimit - lowerScaleLimit);
        }
    }
    
    public class CreatureScaleHelper : ScaleHelper {
        public Creature creature;

        private float? moveActionReachDistance = 0;
        private float originalMaxHealth;
        private float originalHeight;
        public void Awake() {
            creature = GetComponent<Creature>();
            originalHeight = creature.morphology.height;
            creature.OnDespawnEvent += time => {
                if (time == EventTime.OnStart) {
                    ResetScale();
                    Destroy(this);
                }
            };
            if (creature.brain.instance.GetModule<BrainModuleMove>() != null)
                moveActionReachDistance = creature.brain.instance.GetModule<BrainModuleMove>().navReachDistance;
            originalMaxHealth = creature.maxHealth;
        }

        public override void SetBounds(float lower, float upper) {
            lowerScaleLimit = lower;
            upperScaleLimit = upper;
        }

        public static void ScaleItem(Item item, float scale) {
            item.transform.localScale = Vector3.one * scale;
            item.rb.ResetCenterOfMass();
            item.handles.ForEach(handle => {
                handle.CalculateReach();
            });
            item.RefreshCollision();
        }

        public override void Set(float scale) {
            currentScale = Mathf.Clamp(scale * currentScale, lowerScaleLimit, upperScaleLimit);
        }

        public override float CurrentRatio() => creature.morphology.height / originalHeight;

        public void SetAnimationBoneToPart(Ragdoll.Bone bone, bool resetNoPartBone = false)
            => creature.ragdoll.CallPrivate("SetAnimationBoneToPart", bone, resetNoPartBone);

        public void SetAnimationBoneToRig(Ragdoll.Bone bone)
            => creature.ragdoll.CallPrivate("SetAnimationBoneToRig", bone);

        public void SetMeshBone(Ragdoll.Bone bone, bool forceParentMesh = false, bool parentAnimation = false)
            => creature.ragdoll.CallPrivate("SetMeshBone", bone, forceParentMesh, parentAnimation);

        public void SetAnimationBoneToRoot(Ragdoll.Bone bone, bool resetToOrgPosition)
            => creature.ragdoll.CallPrivate("SetAnimationBoneToRoot", bone, resetToOrgPosition);

        public void RefreshDestabilized() {
            creature.ragdoll.CancelGetUp(false);
            creature.animator.enabled = creature.ragdoll.state == Ragdoll.State.Destabilized;
            foreach (var part in creature.ragdoll.parts) {
                if ((bool)(Object)part.bone.fixedJoint)
                    Destroy(part.bone.fixedJoint);
                part.collisionHandler.RemovePhysicModifier(creature.ragdoll);
                if (creature.ragdoll.state == Ragdoll.State.Inert || part == creature.ragdoll.rootPart) {
                    part.bone.SetPinPositionForce(0.0f, 0.0f, 0.0f);
                    part.bone.SetPinRotationForce(0.0f, 0.0f, 0.0f);
                } else {
                    part.bone.SetPinPositionForce(0.0f, 0.0f, 0.0f);
                    part.bone.SetPinRotationForce(
                        creature.ragdoll.springRotationForce * creature.ragdoll.destabilizedSpringRotationMultiplier,
                        creature.ragdoll.damperRotationForce * creature.ragdoll.destabilizedDamperRotationMultiplier,
                        creature.ragdoll.maxRotationForce);
                }
            }

            creature.ragdoll.SavePartsPosition();
            creature.ragdoll.ResetPartsToOrigin();
            foreach (Ragdoll.Bone bone in creature.ragdoll.bones)
            {
              if (bone.parent != null)
              {
                if (bone.hasChildAnimationJoint)
                {
                  bone.animation.SetParent(bone.parent.animation);
                  bone.animation.localPosition = bone.orgLocalPosition;
                  bone.animation.localRotation = bone.orgLocalRotation;
                  bone.animation.localScale = Vector3.one;
                }
                else
                    SetAnimationBoneToPart(bone);
              }
              SetMeshBone(bone);
            }
            foreach (var part in creature.ragdoll.parts)
            {
              part.gameObject.SetActive(true);
              part.bone.animationJoint.gameObject.SetActive(true);
            }
            creature.ragdoll.LoadPartsPosition();
        }

        public void RefreshStanding() {
            creature.animator.enabled = true;
            creature.animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            foreach (var part in creature.ragdoll.parts) {
                if (part.bone.fixedJoint)
                    Destroy(part.bone.fixedJoint);
                part.collisionHandler.SetPhysicModifier(creature.ragdoll, 0.0f);
                if (creature.ragdoll.hipsAttached) {
                    if (part == creature.ragdoll.rootPart) {
                        part.bone.SetPinPositionForce(
                            creature.ragdoll.springPositionForce
                            * creature.ragdoll.hipsAttachedSpringPositionMultiplier,
                            creature.ragdoll.damperPositionForce
                            * creature.ragdoll.hipsAttachedDamperPositionMultiplier,
                            creature.ragdoll.maxPositionForce * creature.ragdoll.hipsAttachedSpringPositionMultiplier);
                        part.bone.SetPinRotationForce(
                            creature.ragdoll.springRotationForce
                            * creature.ragdoll.hipsAttachedSpringRotationMultiplier,
                            creature.ragdoll.damperRotationForce
                            * creature.ragdoll.hipsAttachedDamperRotationMultiplier,
                            creature.ragdoll.maxRotationForce * creature.ragdoll.hipsAttachedSpringRotationMultiplier);
                    } else {
                        part.bone.SetPinPositionForce(0.0f, 0.0f, 0.0f);
                        part.bone.SetPinRotationForce(creature.ragdoll.springRotationForce,
                            creature.ragdoll.damperRotationForce, creature.ragdoll.maxRotationForce);
                    }
                } else {
                    part.bone.SetPinPositionForce(creature.ragdoll.springPositionForce,
                        creature.ragdoll.damperPositionForce, creature.ragdoll.maxPositionForce);
                    part.bone.SetPinRotationForce(creature.ragdoll.springRotationForce,
                        creature.ragdoll.damperRotationForce, creature.ragdoll.maxRotationForce);
                }
            }

            creature.ragdoll.SavePartsPosition();
            creature.ragdoll.ResetPartsToOrigin();
            foreach (Ragdoll.Bone bone in creature.ragdoll.bones) {
                SetAnimationBoneToRig(bone);
                SetMeshBone(bone);
            }

            foreach (RagdollPart part in creature.ragdoll.parts) {
                part.gameObject.SetActive(true);
                part.bone.animationJoint.gameObject.SetActive(true);
            }

            creature.ragdoll.LoadPartsPosition();
            creature.ragdoll.creature.locomotion.enabled = true;
            creature.ragdoll.creature.SetAnimatorHeightRatio(1f);
        }

        public void RefreshNoPhysic() {
            creature.ragdoll.CancelGetUp(false);
            foreach (var part in creature.ragdoll.parts) {
                if ((bool)(Object)part.bone.fixedJoint)
                    Destroy(part.bone.fixedJoint);
                foreach (var holder in part.collisionHandler.holders)
                    holder.transform.SetParent(part.bone.animation.transform, false);
                part.gameObject.SetActive(false);
            }

            foreach (var bone in creature.ragdoll.bones) {
                SetAnimationBoneToRoot(bone, true);
                SetMeshBone(bone, parentAnimation: true);
            }

            foreach (var part in creature.ragdoll.parts) {
                part.transform.SetParent(part.bone.animation.transform);
                part.transform.localPosition = Vector3.zero;
                part.transform.localRotation = Quaternion.identity;
                part.transform.localScale = Vector3.one;
            }

            creature.ragdoll.creature.animator.enabled = true;
            creature.ragdoll.creature.animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            creature.ragdoll.creature.locomotion.enabled = true;
            creature.ragdoll.creature.SetAnimatorHeightRatio(1f);
        }

        public override float Scale(float scale) {
            float size = Mathf.Clamp(scale * currentScale, lowerScaleLimit, upperScaleLimit);
            float newScale = scale * currentScale;
            
            // creature.ragdoll.SavePartsPosition();

            // creature.ragdoll.CancelGetUp();
            // 
            // var oldState = creature.ragdoll.state;
            // creature.ragdoll.SetBodyPositionToHips();
            // 
            // creature.ragdoll.SetState(Ragdoll.State.Disabled);
            // creature.ragdoll.gameObject.SetActive(false);
            // creature.transform.localScale = Vector3.one * size;
            // creature.ragdoll.gameObject.SetActive(true);
            // creature.ragdoll.SetState(oldState);
            // 
            // creature.ragdoll.LoadPartsPosition();
            // creature.animator.SetBool(Creature.hashTstance, false);

            // creature.SetAnimatorHeightRatio(1);
            // creature.ragdoll.RefreshPartJointAndCollision();
            // creature.RefreshCollisionOfGrabbedItems();

            // creature.ragdoll.SetState(creature.ragdoll.state, true);

            creature.SetHeight(creature.morphology.height * size);
            float healthRatio = creature.currentHealth / creature.maxHealth;
            creature.maxHealth = originalMaxHealth * size;
            creature.currentHealth = healthRatio * creature.maxHealth;
            creature.locomotion.SetSpeedModifier(this, newScale, newScale, newScale, newScale, newScale);

            if (creature.handLeft?.grabbedHandle is Handle handleLeft && handleLeft) {
                creature.handLeft.TryRelease();
                ScaleItem(handleLeft.item, size);
                creature.handLeft.Grab(handleLeft);
            }

            if (creature.handRight?.grabbedHandle is Handle handleRight && handleRight) {
                creature.handRight.TryRelease();
                ScaleItem(handleRight.item, size);
                creature.handRight.Grab(handleRight);
            }

            if (creature.equipment?.GetHeldWeapon(Side.Left) is Item itemLeft)
                ScaleItem(itemLeft, size);
            if (creature.equipment?.GetHeldWeapon(Side.Right) is Item itemRight)
                ScaleItem(itemRight, size);

            if (moveActionReachDistance is float moveDistance
                && creature.brain.instance.GetModule<BrainModuleMove>() != null)
                creature.brain.instance.GetModule<BrainModuleMove>().navReachDistance = moveDistance * size;
            return (size - lowerScaleLimit) / (upperScaleLimit - lowerScaleLimit);
        }
    }
}
