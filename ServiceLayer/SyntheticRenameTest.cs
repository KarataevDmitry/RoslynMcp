// Синтетический тест для roslyn_rename: 5 вхождений SyntheticRenameTarget.
namespace RoslynMcp.ServiceLayer;

public static class SyntheticRenameTest
{
    public static int Use(SyntheticRenameTarget a, SyntheticRenameTarget b)
    {
        SyntheticRenameTarget c = a;
        return SyntheticRenameTarget.StaticValue + c.Value;
    }
}

public class SyntheticRenameTarget
{
    public static int StaticValue => 1;
    public int Value => 2;
}
