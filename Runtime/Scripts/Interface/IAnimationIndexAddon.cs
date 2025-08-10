namespace ProjectCI_Animation.Runtime.Interface
{
    public interface IAnimationIndexAddon
    {
        string[] AdditionalIndexNames { get; }
        int GetOriginalIndexByName(string animName);
    }
} 