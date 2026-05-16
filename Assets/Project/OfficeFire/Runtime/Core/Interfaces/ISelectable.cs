namespace Woi.OfficeFire
{
    public interface ISelectable
    {
        bool IsSelectable { get; }

        void Select(SelectionContext context);
    }
}
