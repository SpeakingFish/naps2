using Eto.Drawing;
using Eto.Forms;

namespace NAPS2.EtoForms.Widgets;

public abstract class ListViewBehavior<T> where T : notnull
{
    public bool MultiSelect { get; protected set; }
        
    public bool ShowLabels { get; protected set; }

    public bool ScrollOnDrag { get; protected set; }

    public bool UseHandCursor { get; protected set; }

    public bool Checkboxes { get; protected set; }

    public virtual string GetLabel(T item) => throw new NotSupportedException();

    public virtual Image GetImage(T item, int imageSize) => throw new NotSupportedException();

    public virtual bool AllowDragDrop => false;

    public virtual bool AllowFileDrop => false;

    public virtual string CustomDragDataType => throw new NotSupportedException();

    public virtual byte[] SerializeCustomDragData(T[] items) => throw new NotSupportedException();

    public virtual byte[] MergeCustomDragData(byte[][] dataItems) => throw new NotSupportedException();

    public virtual DragEffects GetCustomDragEffect(byte[] data) => throw new NotSupportedException();
}