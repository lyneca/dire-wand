using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ExtensionMethods;
using ThunderRoad;
using UnityEngine;

namespace Wand {
    public class Grenade : WandModule {
        protected GameObject tipFollower;
        protected EffectInstance spellChargeEffect;
        protected SpellCastCharge capturedSpell;
        protected SpellGrenade grenade;
        public override void OnInit() {
            base.OnInit();
            wand.trigger.Then(() => Vector3.Distance(wand.tip.position, wand.otherHand.caster.magic.position) < 0.03f
                                    && wand.otherHand.caster.isFiring
                                    && (wand.otherHand.caster.spellInstance is SpellCastGravity
                                        || wand.otherHand.caster.spellInstance is SpellCastProjectile
                                        || wand.otherHand.caster.spellInstance is SpellCastLightning), "Touch Spell Orb")
                .Do(GrabGrenade, "Grab Grenade")
                .Then(() => wand.tipViewVelocity.z > wand.module.gestureVelocityNormal
                            && !wand.localTipVelocity.MostlyZ(), "Throw")
                .Do(ThrowGrenade, "Throw Grenade");
            tipFollower = new GameObject();
        }

        public override void OnUpdate() {
            base.OnUpdate();
            tipFollower.transform.position
                = Vector3.Lerp(tipFollower.transform.position, wand.tip.position, Time.deltaTime * 20);
        }

        public override void OnReset() {
            base.OnReset();
            spellChargeEffect?.End();
            spellChargeEffect = null;
        }

        public void GrabGrenade() {
            var caster = wand.otherHand.caster;
            var spell = caster.spellInstance as SpellCastCharge;
            var effect = caster.spellInstance.GetField<EffectInstance>("chargeEffectInstance");
            var newInstance = spell.GetField<EffectData>("chargeEffectData").Spawn(caster.magic.position,
                caster.magic.rotation, caster.magic, null, true);
            newInstance.Play();
            spell.SetField("chargeEffectInstance", newInstance);
            spell.currentCharge = 0;
            tipFollower.transform.position = caster.magic.position;
            capturedSpell = spell;
            spellChargeEffect = effect;
            grenade = wand.objectPool.Get().AddComponent<SpellGrenade>();
            grenade.transform.position = tipFollower.transform.position;
            effect.SetParent(grenade.transform);
            effect.SetPosition(grenade.transform.position);
            grenade.transform.SetParent(tipFollower.transform);
            grenade.effect = spellChargeEffect;
            grenade.wand = wand;
            grenade.spell = capturedSpell;
            grenade.Init();
        }

        public void ThrowGrenade() {
            grenade.Throw(Vector3.Slerp(wand.tipVelocity.normalized, Player.local.head.transform.forward, 0.5f)
                          * (wand.tipVelocity.magnitude * 5));
        }

    }
        public class SpellGrenade : MonoBehaviour {
            public EffectInstance effect;
            public SpellCastCharge spell;
            public Rigidbody rb;
            public WandBehaviour wand;
            public SphereCollider collider;

            public void Init() {
                collider = gameObject.AddComponent<SphereCollider>();
                rb = gameObject.GetOrAddComponent<Rigidbody>();
                rb.isKinematic = true;
                wand.item.IgnoreCollider(collider);
                collider.radius = 0.06f;
                collider.enabled = false;
            }

            public void Throw(Vector3 velocity) {
                transform.SetParent(null);
                rb.isKinematic = false;
                collider.enabled = true;
                rb.AddForce(velocity, ForceMode.VelocityChange);
            }

            public void OnCollisionEnter(Collision collision) {
                if (collision.rigidbody?.GetComponentInParent<WandBehaviour>() != null)
                    return;
                Explode(collision);
                effect.End();
                wand.objectPool.Release(gameObject);
            }

            public void Explode(Collision collision) {
                if (spell is SpellCastGravity gravity) {
                    GameManager.local.StartCoroutine(gravity.CallPrivate("ShockWaveCoroutine",
                        collision.GetContact(0).point,
                        collision.GetContact(0).normal, collision.collider.transform.up,
                        collision.relativeVelocity) as IEnumerator);
                } else if (spell is SpellCastProjectile fire) {
                    Utils.Explosion(collision.GetContact(0).point, 40, 4, true, true, true, false, 20);
                    var explosion = wand.module.explosionEffectData
                        .Spawn(collision.GetContact(0).point, Quaternion.identity, null, null, false);
                    explosion.SetVFXProperty("Size", 4);
                    explosion.Play();
                }
            }
        }
}
