public class Cantrip : Module {
    public override void OnInit() {
        wand.targetedItem
            .Then(() => wand.item.mainHandler?.Buttoning())
            .Do(Boop);
    }

    public void Boop() {
        if (wand.target.item == null) return;
        
        var item = wand.target.item;

        if (item.GetComponent<HingeDrive>() is HingeDrive drive) {
            drive.Unlock();
        }
    }
}
