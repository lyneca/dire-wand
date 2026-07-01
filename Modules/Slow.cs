using ExtensionMethods;
using GestureEngine;
using ThunderRoad;

namespace Wand; 

public class Slow : WandModule {
    public override void OnInit() {
        base.OnInit();

        wand.OnTargetEntity(step
            => step
                .Then(wand.Offhand.Moving(Direction.Forward).Palm(Direction.Forward).Open)
                .Do(SlowEntity));
    }

    public void SlowEntity() {
        MarkCasted();
        Catalog.GetData<EffectData>("WandSlow")?.Spawn(wand.target.Transform).Play();
        wand.target?.Inflict<Slowed>(this, 10);
    }
}
