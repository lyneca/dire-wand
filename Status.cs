using System;
using System.Collections.Generic;
using System.Linq;
using ExtensionMethods;
using ThunderRoad;
using UnityEngine;

namespace Wand;

public interface IStatus {
    EffectInstance SpawnEffect(Transform parent, float size);
    void Apply(Item item);
    void Remove(Item item);
    void Apply(Creature creature);
    void Remove(Creature creature);
    void Update(Item item);
    void Update(Creature creature);
}

public abstract class Status : IStatus {
    public string effectId;

    public EffectInstance SpawnEffect(Transform parent, float size) {
        if (effectId == null) return null;
        var effect = Catalog.GetData<EffectData>(effectId)?.Spawn(parent);
        if (effect == null) return null;
        foreach (var particle in effect.effects.OfType<EffectParticle>()) {
            particle.transform.localScale = Vector3.one * size;
        }

        return effect;
    }

    public virtual void Apply(Creature creature) { }
    public virtual void Apply(Item item) { }
    public virtual void Remove(Creature creature) { }
    public virtual void Remove(Item item) { }
    public virtual void Update(Item item) { }
    public virtual void Update(Creature creature) { }
}

public static class Extensions {
    public static T Inflict<T>(this Creature creature, object handler, float time = 0, bool withEffect = true) where T : IStatus
        => creature.gameObject.GetOrAddComponent<StatusHandler>().Inflict<T>(handler, time, withEffect);

    public static T Inflict<T>(this Item item, object handler, float time = 0, bool withEffect = true) where T : IStatus
        => item.gameObject.GetOrAddComponent<StatusHandler>().Inflict<T>(handler, time, withEffect);

    public static T Inflict<T>(this Entity entity, object handler, float time = 0, bool withEffect = true) where T : class, IStatus {
        return entity.creature?.Inflict<T>(handler, time, withEffect) ?? entity.item?.Inflict<T>(handler, time, withEffect);
    }
    

    public static void Remove<T>(this Creature creature, object handler) where T : IStatus
        => creature.gameObject.GetOrAddComponent<StatusHandler>().Remove<T>(handler);

    public static void Remove<T>(this Item item, object handler) where T : IStatus
        => item.gameObject.GetOrAddComponent<StatusHandler>().Remove<T>(handler);

    public static void Remove<T>(this Entity entity, object handler) where T : IStatus {
        entity.creature?.Remove<T>(handler);
        entity.item?.Remove<T>(handler);
    }
    

    public static void Clear<T>(this Creature creature) where T : IStatus
        => creature.gameObject.GetOrAddComponent<StatusHandler>().Clear<T>();

    public static void Clear<T>(this Item item) where T : IStatus
        => item.gameObject.GetOrAddComponent<StatusHandler>().Clear<T>();

    public static void Clear<T>(this Entity entity) where T : IStatus {
        entity.creature?.Clear<T>();
        entity.item?.Clear<T>();
    }
    

    public static void ClearAll(this Creature creature)
        => creature.gameObject.GetOrAddComponent<StatusHandler>().ClearAll();

    public static void ClearAll(this Item item)
        => item.gameObject.GetOrAddComponent<StatusHandler>().ClearAll();

    public static void ClearAll(this Entity entity) {
        entity.creature?.ClearAll();
        entity.item?.ClearAll();
    }


    public static bool Has<T>(this Creature creature) where T : IStatus
        => creature.gameObject.GetOrAddComponent<StatusHandler>().Has<T>();

    public static bool Has<T>(this Item item) where T : IStatus
        => item.gameObject.GetOrAddComponent<StatusHandler>().Has<T>();

    public static bool Has<T>(this Entity entity) where T : IStatus
        => entity.isCreature ? entity.creature.Has<T>() : entity.item.Has<T>();

}

public class StatusHandler : MonoBehaviour {
    public Dictionary<Type, HashSet<object>> handlers;
    public Dictionary<IStatus, (float time, EffectInstance effect)> statuses;

    public Item item;
    public Creature creature;
    public Transform effectTransform;

    public bool IsItem => item != null;
    public bool IsCreature => creature != null;

    private void Awake() { Init(); }

    public void Init() {
        creature = GetComponent<Creature>();
        item = GetComponent<Item>();
        effectTransform = new GameObject().transform;
        effectTransform.position = creature?.ragdoll.rootPart.transform.position ?? item.transform.TransformPoint(item.GetLocalCenter());

        if (creature)
            creature.OnDespawnEvent += OnDespawn;
        if (item)
            item.OnDespawnEvent += OnDespawn;

        handlers = new Dictionary<Type, HashSet<object>>();
        statuses = new Dictionary<IStatus, (float time, EffectInstance effect)>();
    }

    public void OnDespawn(EventTime time) {
        if (time != EventTime.OnStart) return;
        try {
            ClearAll();
        } catch {
            // ignored
        }
        
        Destroy(this);

        if (creature)
            creature.OnDespawnEvent -= OnDespawn;
        if (item)
            item.OnDespawnEvent -= OnDespawn;
    }

    public T Inflict<T>(object handler, float time = 0, bool withEffect = true) where T : IStatus {
        var type = typeof(T);
        T status;
        if (handlers.ContainsKey(type)) {
            status = statuses.Keys.OfType<T>().FirstOrDefault();
            RefreshEffect(status, withEffect);
            if (handlers[type].Contains(handler)) return status;
            handlers[type].Add(handler);
        } else {
            handlers[type] = new HashSet<object> { handler };
            status = Activator.CreateInstance<T>();
            ApplyStatus(status, withEffect);
        }

        if (time > 0) {
            this.RunAfter(() => Remove<T>(handler), time);
        }

        return status;
    }

    public void Remove<T>(object handler) where T : IStatus {
        var type = typeof(T);
        if (handlers == null
            || !handlers.ContainsKey(type)
            || handlers[type] == null
            || !handlers[type].Remove(handler)
            || handlers[type].Count > 0) return;
        handlers.Remove(type);
        var status = statuses.Keys.OfType<T>().FirstOrDefault();
        if (status == null) return;
        RemoveStatus(status);
    }

    public bool Has<T>() where T : IStatus => handlers.ContainsKey(typeof(T));

    protected void RefreshEffect(IStatus status, bool withEffect) {
        if (!withEffect || statuses[status].effect != null) return;
        
        EffectInstance effect = null;
        if (IsItem) {
            effect = status.SpawnEffect(effectTransform,
                item.GetLocalBounds().size.magnitude / 2 * item.transform.localScale.x * 1.1f);
        } else if (IsCreature) {
            if (withEffect)
                effect = status.SpawnEffect(effectTransform, 1.5f * creature.transform.localScale.x);
        }
        effect?.Play();
        statuses[status] = (statuses[status].time, effect);
    }

    protected void ApplyStatus(IStatus status, bool withEffect = true) {
        EffectInstance effect = null;
        if (IsItem) {
            status.Apply(item);
            if (withEffect)
                effect = status.SpawnEffect(effectTransform,
                    item.GetLocalBounds().size.magnitude / 2 * item.transform.localScale.x * 1.1f);
        } else if (IsCreature) {
            status.Apply(creature);
            if (withEffect)
                effect = status.SpawnEffect(effectTransform, 1.5f * creature.transform.localScale.x);
        }
        effect?.Play();
        statuses[status] = (Time.time, effect);
    }

    protected void RemoveStatus(IStatus status) {
        handlers.Remove(status.GetType());
        if (IsItem)
            status.Remove(item);
        if (IsCreature)
            status.Remove(creature);
        if (statuses[status].effect != null) {
            statuses[status].effect.onEffectFinished += instance => instance.Despawn();
            statuses[status].effect.End();
        }

        statuses.Remove(status);
    }

    public void Clear<T>() where T: IStatus {
        if (statuses == null) return;
        foreach (var status in statuses.Keys.OfType<T>().ToList()) {
            RemoveStatus(status);
        }
    }

    public void ClearAll() {
        if (statuses == null) return;
        foreach (var status in statuses.Keys.ToList()) {
            RemoveStatus(status);
        }
    }

    public void Reset() {
        StopAllCoroutines();
        ClearAll();
    }

    public void Update() {
        effectTransform.position = creature?.ragdoll.rootPart.transform.position ?? item.transform.TransformPoint(item.GetLocalCenter());
        foreach (var status in statuses.Keys) {
            if (item)
                status.Update(item);
            if (creature)
                status.Update(creature);
        }
    }
}

public class Physical : Status {
    public override void Apply(Creature creature) {
        base.Apply(creature);
        creature.brain.AddNoStandUpModifier(this);
        creature.ragdoll.forcePhysic.Add(this);
    }

    public override void Remove(Creature creature) {
        base.Remove(creature);
        creature.brain.RemoveNoStandUpModifier(this);
        creature.ragdoll.forcePhysic.Remove(this);
    }

    public override void Apply(Item item) {
        base.Apply(item);
        item.Depenetrate();
        item.SetColliderLayer(GameManager.GetLayer(LayerName.MovingItem));
        item.physicBody.collisionDetectionMode = Catalog.gameData.collisionDetection.telekinesis;
        item.forceThrown = true;
        item.Throw();
    }

    public override void Remove(Item item) {
        base.Remove(item);
        item.forceThrown = false;
        item.Throw(1, Item.FlyDetection.Forced);
    }
}

public class ZeroGravity : Physical {
    public override void Apply(Creature creature) {
        base.Apply(creature);
        creature.ragdoll.SetPhysicModifier(this, 0);
    }

    public override void Remove(Creature creature) {
        base.Remove(creature);
        creature.ragdoll.RemovePhysicModifier(this);
    }

    public override void Apply(Item item) {
        base.Apply(item);
        item.SetPhysicModifier(this, 0);
    }

    public override void Remove(Item item) {
        base.Remove(item);
        item.RemovePhysicModifier(this);
    }
}

public class Floating : Physical {
    public Floating() => effectId = "WandStatusHover";

    public override void Apply(Creature creature) {
        base.Apply(creature);
        if (!creature.isKilled)
            creature.ragdoll.SetState(Ragdoll.State.Destabilized);
        creature.ragdoll.SetPhysicModifier(this, 0, -1, 2, 2);
        creature.locomotion.SetPhysicModifier(this, 0, -1, 2);
        creature.ragdoll.rootPart.physicBody.AddForce(Vector3.up * 30, ForceMode.VelocityChange);
        creature.ragdoll.rootPart.physicBody.AddTorque(
            Vector3.Cross((creature.transform.position - Player.local.transform.position).normalized, Vector3.up) * 60f,
            ForceMode.VelocityChange);
    }

    public override void Remove(Creature creature) {
        base.Remove(creature);
        creature.ragdoll.RemovePhysicModifier(this);
        creature.locomotion.RemovePhysicModifier(this);
    }

    public override void Apply(Item item) {
        base.Apply(item);
        item.SetPhysicModifier(this, 0, 1, 2, 2);
        item.physicBody.AddForce(Vector3.up * 3, ForceMode.VelocityChange); 
        item.physicBody.AddTorque(Vector3.Cross((item.transform.position - Player.local.transform.position).normalized, Vector3.up) * 20f, ForceMode.VelocityChange);
    }

    public override void Remove(Item item) {
        base.Remove(item);
        item.RemovePhysicModifier(this);
    }
}

public class Slowed : Status {
    public float amount = 0.2f;
    public Slowed() => effectId = null;  // "WandStatusSlow";

    public override void Apply(Creature creature) {
        base.Apply(creature);
        creature.ragdoll.forcePhysic.Add(this);
        creature.ragdoll.SetPhysicModifier(this, drag: 10);
        creature.animator.speed = amount;
        var speak = creature.brain.instance.GetModule<BrainModuleSpeak>();
        speak.SetField("pitch", speak.GetField<float>("pitch") * amount);
        creature.locomotion.SetSpeedModifier(this, amount, amount, amount, amount, amount);
    }

    public override void Remove(Creature creature) {
        base.Remove(creature);
        creature.ragdoll.forcePhysic.Remove(this);
        creature.ragdoll.RemovePhysicModifier(this);
        creature.animator.speed = 1;
        var speak = creature.brain.instance.GetModule<BrainModuleSpeak>();
        speak.SetField("pitch", speak.GetField<float>("pitch") * (1 / amount));
        creature.locomotion.RemoveSpeedModifier(this);
    }

    public override void Apply(Item item) {
        base.Apply(item);
        foreach (var handler in item.collisionHandlers) {
            handler.physicBody.rigidBody.AddModifier(this, 3, drag: 10);
        }
    }

    public override void Remove(Item item) {
        base.Remove(item);
        foreach (var handler in item.collisionHandlers) {
            handler.physicBody.rigidBody.RemoveModifier(this);
        }
    }
}