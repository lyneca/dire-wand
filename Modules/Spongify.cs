using ExtensionMethods;
using GestureEngine;
using ThunderRoad;
using UnityEngine;

namespace Wand; 

public class Spongify : WandModule {
    public string bounceEffectId = "WandThrum";
    public EffectData bounceEffectData;

    public override void OnInit() {
        base.OnInit();
        bounceEffectData = Catalog.GetData<EffectData>(bounceEffectId);
        wand.targetedItem
            .Then(wand.Offhand.Fist.Palm(Direction.Inwards).Thumb(Direction.Up).Moving(Direction.Down))
            .Then(wand.Offhand.Fist.Palm(Direction.Inwards).Thumb(Direction.Up).Still)
            // .Then(wand.Swirl(SwirlDirection.CounterClockwise))
            .Do(SpongifyItem, "Spongify Item");
    }

    public void SpongifyItem() {
        wand.target?.item?.gameObject.GetComponent<FreezeModifier>()?.Clear();
        wand.target?.item?.gameObject.GetOrAddComponent<BounceBehaviour>().Activate(this);
    }
}

public class BounceBehaviour : MonoBehaviour {
    private Item item;
    public bool active;
    private Spongify module;

    public void Activate(Spongify module) {
        if (item == null) return;
        this.module = module;
        active = true;
        item.rb.AddForce(Vector3.up * (6 * Random.Range(1f, 3f)), ForceMode.VelocityChange);
    }

    public void Deactivate() => active = false;

    public void Awake() {
        item = gameObject.GetComponentInParent<Item>();
        if (!item) return;
        item.OnDespawnEvent += (time) => {
            if (time == EventTime.OnStart) {
                Destroy(this);
            }
        };
        item.mainCollisionHandler.OnCollisionStartEvent += OnCollision;
    }

    public void OnCollision(CollisionInstance collision) {
        module.bounceEffectData.Spawn(collision.contactPoint, Quaternion.LookRotation(collision.contactNormal))
            .Play();
        if (active) item.rb.velocity = Vector3.Reflect(collision.impactVelocity * 1.1f, collision.contactNormal);
    }
}