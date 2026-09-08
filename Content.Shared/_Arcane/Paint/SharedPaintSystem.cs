namespace Content.Shared._Arcane.Paint;

public abstract class SharedPaintSystem : EntitySystem
{
    public virtual void UpdateAppearance(EntityUid uid, ArcanePaintedComponent? component = null) { }
}
