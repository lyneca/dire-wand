using ExtensionMethods;
using GestureEngine;
using ThunderRoad;

namespace Wand; 

public class Slow : WandSkill {
    public override void Register() {
        base.Register();

        wand.OnTargetEntity(step
            => step
                .Then(wand.Offhand.At(Position.Chest).Moving(Direction.Forward).Palm(Direction.Forward).Gripping)
                .Do(SlowEntity));
    }

    public void SlowEntity() {
        MarkCasted();
        Catalog.GetData<EffectData>("WandSlow")?.Spawn(wand.TargetTransform).Play();
        wand.target?.Inflict("Slowed", this, 10);
    }
}
