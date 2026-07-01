using ExtensionMethods;
using ThunderRoad;
using UnityEngine;

namespace Wand;

public class Align : WandModule {
    public override void OnInit() {
        base.OnInit();
        wand.trigger.Then(wand.Swirl(SwirlDirection.Clockwise)).Then(wand.Brandish()).Do(AlignPlayer);
    }

    public void AlignPlayer() {
        if (Physics.Raycast(wand.tipRay, out var hit, 30,
                Utils.GetMask(LayerName.Default, LayerName.LocomotionOnly, LayerName.None),
                QueryTriggerInteraction.Ignore)) {
            var source = new GameObject().transform;
            source.SetPositionAndRotation(wand.tip.position, wand.tip.rotation);
            var target = new GameObject().transform;
            target.SetPositionAndRotation(hit.point, Quaternion.LookRotation(hit.normal));
            var effect = Catalog.GetData<EffectData>("TargetLine").Spawn(source);
            effect.SetSource(source);
            effect.SetTarget(target);
            effect.Play();
            Player.local.RunAfter(() => {
                
                Player.currentCreature.ragdoll.transform.SetParent(Player.local.transform);
                
                // if (LiveValue.Get<bool>("align", true))
                //     Player.currentCreature.ragdoll.transform.rotation
                //         = Quaternion.FromToRotation(Vector3.up, hit.normal)
                //           * Player.currentCreature.ragdoll.transform.rotation;
                // else
                //     Player.currentCreature.ragdoll.transform.rotation
                //         = Quaternion.Inverse(Quaternion.FromToRotation(Vector3.up, hit.normal))
                //           * Player.currentCreature.ragdoll.transform.rotation;
                Player.local.autoAlign = true;
                Player.local.autoAlignSpeed = 10;
                Player.local.autoAlignDirection = hit.normal;
                Player.currentCreature.gameObject.GetOrAddComponent<GravityModifier>().SetGravity(hit.normal);
                // Player.local.Teleport(hit.point + hit.normal * 0.3f,
                //     Quaternion.LookRotation(Vector3.Cross(Player.local.transform.forward, hit.normal)));
            }, 0.3f);
        }
    }

    public class GravityModifier : MonoBehaviour {
        private bool custom;
        private Vector3 up;
        private Creature creature;

        public void Awake() {
            creature = GetComponent<Creature>();
        }

        public void SetGravity(Vector3 up) {
            this.up = up;
            if (up == Vector3.up) {
                custom = false;
                creature.currentLocomotion.RemovePhysicModifier(this);
            } else {
                custom = true;
                creature.currentLocomotion.SetPhysicModifier(this, 0);
            }
        }

        public void Update() {
            if (custom)
                creature.currentLocomotion.physicBody.AddForce(-up * Physics.gravity.magnitude, ForceMode.Acceleration);
        }
    }
}
