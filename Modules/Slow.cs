using ExtensionMethods;
using GestureEngine;

namespace Wand; 

public class Slow : WandModule {
    public override void OnInit() {
        base.OnInit();
            
        wand.targetedItem
            .Then(wand.Offhand.Moving(Direction.Forward).Palm(Direction.Forward).Open)
            .Do(SlowItem);
        wand.targetedEnemy
            .Then(wand.Offhand.Moving(Direction.Forward).Palm(Direction.Forward).Open)
            .Do(SlowCreature);
    }

    public void SlowCreature() {
        MarkCasted();
        wand.target.creature.gameObject.GetOrAddComponent<SlowCreatureModifier>().AddHandler(this);
        wand.target.creature.RunAfter(
            () => wand.target.creature.gameObject.GetOrAddComponent<SlowCreatureModifier>().RemoveHandler(this), 10);
    }

    public void SlowItem() {
        MarkCasted();
        foreach (var handler in wand.target.item.collisionHandlers) {
            handler.rb.AddModifier(this, 3, drag: 10);
        }

        wand.target.item.RunAfter(() => {
            foreach (var handler in wand.target.item.collisionHandlers) {
                handler.rb.RemoveModifier(this);
            }
        }, 10);

    }
    
}
